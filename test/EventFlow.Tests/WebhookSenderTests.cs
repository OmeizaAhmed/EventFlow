using System.Net;
using System.Text;
using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Infrastructure;
using EventFlow.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventFlow.Tests;

public class WebhookSenderTests
{
    private static readonly WebhookSendRequest Request = new(
        Guid.NewGuid(), Guid.NewGuid(), "order.created", "https://example.com/webhook", "{\"name\":\"café\"}", "secret");

    [Theory]
    [InlineData(200, WebhookSendOutcome.Success)]
    [InlineData(204, WebhookSendOutcome.Success)]
    [InlineData(302, WebhookSendOutcome.PermanentFailure)]
    [InlineData(400, WebhookSendOutcome.PermanentFailure)]
    [InlineData(408, WebhookSendOutcome.RetryableFailure)]
    [InlineData(425, WebhookSendOutcome.RetryableFailure)]
    [InlineData(429, WebhookSendOutcome.RetryableFailure)]
    [InlineData(503, WebhookSendOutcome.RetryableFailure)]
    public async Task Send_ClassifiesResponse(int status, WebhookSendOutcome expected)
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        var result = await Sender(client).SendAsync(Request);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task Send_PreservesPayloadAndSignsHeaders()
    {
        var time = new TestTimeProvider();
        using var client = Client(async (message, ct) =>
        {
            Assert.Equal(HttpMethod.Post, message.Method);
            Assert.Equal(Encoding.UTF8.GetBytes(Request.Payload), await message.Content!.ReadAsByteArrayAsync(ct));
            Assert.Equal("application/json", message.Content.Headers.ContentType!.MediaType);
            Assert.Equal(Request.DeliveryId.ToString(), message.Headers.GetValues("EventFlow-Delivery-Id").Single());
            var timestamp = time.GetUtcNow().ToUnixTimeSeconds();
            Assert.Equal($"t={timestamp},v1={new WebhookSignatureGenerator().Sign(Request.Secret, timestamp, Request.Payload)}",
                message.Headers.GetValues("EventFlow-Signature").Single());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        Assert.Equal(time.GetUtcNow(), (await Sender(client, time: time).SendAsync(Request)).AttemptedAt);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(3, "abc")]
    [InlineData(100, "abcdef")]
    public async Task Send_RespectsExactBodyLimit(int limit, string expected)
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("abcdef")
        }));
        var result = await Sender(client, new() { MaxResponseBodyBytes = limit }).SendAsync(Request);
        Assert.Equal(expected, result.ResponseBody);
    }

    [Theory]
    [InlineData("not a URL")]
    [InlineData("ftp://example.com/file")]
    public async Task Send_InvalidUrlDoesNotSend(string url)
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Must not send"));
        Assert.Equal(WebhookSendOutcome.PermanentFailure,
            (await Sender(client).SendAsync(Request with { Url = url })).Outcome);
    }

    [Fact]
    public async Task Send_CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = Client(async (_, ct) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sender(client).SendAsync(Request, cancellation.Token));
    }

    [Fact]
    public async Task Send_TimeoutIsRetryable()
    {
        using var client = Client(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        var result = await Sender(client, new() { Timeout = TimeSpan.FromMilliseconds(25) }).SendAsync(Request);
        Assert.Equal(WebhookSendOutcome.RetryableFailure, result.Outcome);
        Assert.Contains("timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task Send_NetworkFailureIsRetryable()
    {
        using var client = Client((_, _) => throw new HttpRequestException("Connection refused"));
        Assert.Equal(WebhookSendOutcome.RetryableFailure, (await Sender(client).SendAsync(Request)).Outcome);
    }

    [Fact]
    public async Task Send_ResponseReadFailureIsRetryable()
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new BrokenStream())
        }));
        Assert.Equal(WebhookSendOutcome.RetryableFailure, (await Sender(client).SendAsync(Request)).Outcome);
    }

    [Theory]
    [InlineData(30, 20)]
    [InlineData(5, 0)]
    public async Task Send_RetryAfterDateUsesCurrentTime(int retrySeconds, int expectedSeconds)
    {
        var time = new TestTimeProvider();
        var retryAt = time.GetUtcNow().AddSeconds(retrySeconds);
        using var client = Client((_, _) =>
        {
            time.Now = time.Now.AddSeconds(10);
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(retryAt);
            return Task.FromResult(response);
        });
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), (await Sender(client, time: time).SendAsync(Request)).RetryAfter);
    }

    [Fact]
    public void Registration_ResolvesSenderAndPreservesCustomClock()
    {
        var clock = new TestTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddEventFlowInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.IsType<WebhookSender>(provider.GetRequiredService<IWebhookSender>());
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Theory]
    [InlineData("Timeout", "00:00:00")]
    [InlineData("MaxResponseBodyBytes", "-1")]
    [InlineData("UserAgent", "invalid\r\nheader")]
    public void Registration_RejectsInvalidConfiguration(string key, string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [$"WebhookSender:{key}"] = value }).Build();
        var services = new ServiceCollection().AddEventFlowInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IWebhookSender>());
    }

    private static WebhookSender Sender(HttpClient client, WebhookSenderOptions? options = null, TimeProvider? time = null) =>
        new(client, new WebhookSignatureGenerator(), Options.Create(options ?? new()), time ?? TimeProvider.System,
            NullLogger<WebhookSender>.Instance);

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send));

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class BrokenStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Connection reset"));
    }
}
