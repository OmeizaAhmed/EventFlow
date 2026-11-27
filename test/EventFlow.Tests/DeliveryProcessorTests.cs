using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SendOutcome = EventFlow.Application.DTOs.WebhookSendOutcome;

namespace EventFlow.Tests;

public class DeliveryProcessorTests
{
    [Fact]
    public async Task NoDueDelivery_DoesNotSendOrSave()
    {
        var fixture = new Fixture(hasDelivery: false);

        Assert.False(await fixture.Processor.ProcessNextAsync());

        Assert.Empty(fixture.Sender.Requests);
        Assert.Equal(0, fixture.Repository.SaveCalls);
        Assert.Equal(fixture.Clock.Now.UtcDateTime, fixture.Repository.LastClaimAt);
        Assert.Equal(fixture.Options.LeaseDuration, fixture.Repository.LastLeaseDuration);
    }

    [Fact]
    public async Task Success_SendsStoredPayloadAndDecryptedSecretAndPersistsOutcome()
    {
        var fixture = new Fixture();
        var result = fixture.Result(SendOutcome.Success, 202) with
        {
            ResponseBody = "{\"accepted\":true}",
            Duration = TimeSpan.FromMilliseconds(3123),
            AttemptedAt = fixture.Clock.Now.AddSeconds(1)
        };
        fixture.Sender.Send = (_, _) => Task.FromResult(result);

        Assert.True(await fixture.Processor.ProcessNextAsync());

        var request = Assert.Single(fixture.Sender.Requests);
        Assert.Equal(fixture.Delivery.DeliveryId, request.DeliveryId);
        Assert.Equal(fixture.Delivery.Event.EventId, request.EventId);
        Assert.Equal("order.created", request.EventType);
        Assert.Equal("https://example.com/webhook", request.Url);
        Assert.Equal("{ \"reference\": \"caf\u00e9\", \"amount\": 10.00 }", request.Payload);
        Assert.Equal("plain-signing-secret", request.Secret);
        Assert.Equal("encrypted-signing-secret", fixture.Keys.LastEncryptedValue);

        var attempt = Assert.Single(fixture.Delivery.Attempts);
        Assert.NotEqual(Guid.Empty, attempt.DeliveryAttemptId);
        Assert.Equal(fixture.Delivery.DeliveryId, attempt.DeliveryId);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.True(attempt.Succeeded);
        Assert.Equal(202, attempt.HttpStatusCode);
        Assert.Equal(result.ResponseBody, attempt.ResponseBody);
        Assert.Null(attempt.ErrorMessage);
        Assert.Equal(3123L, attempt.DurationMilliseconds);
        Assert.Equal(result.AttemptedAt.UtcDateTime, attempt.AttemptedAt);
        Assert.Equal(DeliveryStatus.Delivered, fixture.Delivery.Status);
        Assert.Null(fixture.Delivery.NextRetryAt);
        Assert.Null(fixture.Delivery.LeaseToken);
        Assert.Null(fixture.Delivery.LeaseExpiresAt);
        Assert.Equal(fixture.Repository.LastIssuedLease, fixture.Repository.LastSavedLease);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        Assert.False(await fixture.Processor.ProcessNextAsync());
        Assert.Single(fixture.Sender.Requests);
    }

    [Fact]
    public async Task RetryThenSuccess_PreservesDeliveryIdentityAndAttemptHistory()
    {
        var fixture = new Fixture();
        fixture.Sender.Send = (_, _) => Task.FromResult(fixture.Result(SendOutcome.RetryableFailure, 503) with
        {
            ResponseBody = "temporarily unavailable",
            ErrorMessage = "HTTP 503"
        });

        Assert.True(await fixture.Processor.ProcessNextAsync());

        Assert.Equal(DeliveryStatus.RetryScheduled, fixture.Delivery.Status);
        var retryAt = Assert.IsType<DateTime>(fixture.Delivery.NextRetryAt);
        var delay = retryAt - fixture.Clock.Now.UtcDateTime;
        Assert.InRange(delay, fixture.Options.InitialRetryDelay / 2, fixture.Options.InitialRetryDelay);
        var firstAttempt = Assert.Single(fixture.Delivery.Attempts);
        Assert.False(firstAttempt.Succeeded);
        Assert.Equal("temporarily unavailable", firstAttempt.ResponseBody);
        Assert.Equal("HTTP 503", firstAttempt.ErrorMessage);
        Assert.False(await fixture.Processor.ProcessNextAsync());
        Assert.Single(fixture.Sender.Requests);

        fixture.Clock.Now = new DateTimeOffset(retryAt);
        fixture.Sender.Send = (_, _) => Task.FromResult(fixture.Result(SendOutcome.Success, 200));
        Assert.True(await fixture.Processor.ProcessNextAsync());

        Assert.Equal(DeliveryStatus.Delivered, fixture.Delivery.Status);
        Assert.Equal(new[] { 1, 2 }, fixture.Delivery.Attempts.Select(attempt => attempt.AttemptNumber));
        Assert.Equal(fixture.Sender.Requests[0], fixture.Sender.Requests[1]);
        Assert.Same(firstAttempt, fixture.Delivery.Attempts.First());
        Assert.True(fixture.Delivery.Attempts.Last().Succeeded);
        Assert.Null(fixture.Delivery.NextRetryAt);
    }

    [Theory]
    [InlineData(120, 120)]
    [InlineData(7200, 3600)]
    public async Task RetryAfter_IsRespectedWithinConfiguredMaximum(int requestedSeconds, int expectedSeconds)
    {
        var fixture = new Fixture();
        fixture.Sender.Send = (_, _) => Task.FromResult(fixture.Result(SendOutcome.RetryableFailure, 429) with
        {
            RetryAfter = TimeSpan.FromSeconds(requestedSeconds)
        });

        Assert.True(await fixture.Processor.ProcessNextAsync());

        Assert.Equal(fixture.Clock.Now.UtcDateTime.AddSeconds(expectedSeconds), fixture.Delivery.NextRetryAt);
        Assert.Equal(DeliveryStatus.RetryScheduled, fixture.Delivery.Status);
    }

    [Fact]
    public async Task RepeatedFailures_StopAtMaximumAttempts()
    {
        var fixture = new Fixture();
        fixture.Options.MaxAttempts = 3;
        fixture.Sender.Send = (_, _) => Task.FromResult(fixture.Result(SendOutcome.RetryableFailure, 503));

        for (var attempt = 1; attempt <= fixture.Options.MaxAttempts; attempt++)
        {
            Assert.True(await fixture.Processor.ProcessNextAsync());
            Assert.Equal(attempt, fixture.Delivery.AttemptCount);
            if (attempt < fixture.Options.MaxAttempts)
            {
                Assert.Equal(DeliveryStatus.RetryScheduled, fixture.Delivery.Status);
                fixture.Clock.Now = new DateTimeOffset(Assert.IsType<DateTime>(fixture.Delivery.NextRetryAt));
            }
        }

        Assert.Equal(DeliveryStatus.DeadLetter, fixture.Delivery.Status);
        Assert.Null(fixture.Delivery.NextRetryAt);
        Assert.Null(fixture.Delivery.LeaseToken);
        Assert.False(await fixture.Processor.ProcessNextAsync());
        Assert.Equal(3, fixture.Sender.Requests.Count);
        Assert.Equal(new[] { 1, 2, 3 }, fixture.Delivery.Attempts.Select(attempt => attempt.AttemptNumber));
    }

    [Fact]
    public async Task PermanentFailure_GoesDirectlyToDeadLetter()
    {
        var fixture = new Fixture();
        fixture.Sender.Send = (_, _) => Task.FromResult(fixture.Result(SendOutcome.PermanentFailure, 400));

        Assert.True(await fixture.Processor.ProcessNextAsync());

        Assert.Equal(DeliveryStatus.DeadLetter, fixture.Delivery.Status);
        Assert.Single(fixture.Delivery.Attempts);
        Assert.Null(fixture.Delivery.NextRetryAt);
        Assert.False(await fixture.Processor.ProcessNextAsync());
        Assert.Single(fixture.Sender.Requests);
    }

    [Fact]
    public async Task CallerCancellation_LeavesClaimRecoverableWithoutRecordingFailure()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Sender.Send = (_, ct) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<WebhookSendResult>(ct);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Processor.ProcessNextAsync(cancellation.Token));

        Assert.Equal(DeliveryStatus.Processing, fixture.Delivery.Status);
        Assert.Empty(fixture.Delivery.Attempts);
        Assert.NotNull(fixture.Delivery.LeaseToken);
        Assert.Equal(fixture.Clock.Now.UtcDateTime.Add(fixture.Options.LeaseDuration), fixture.Delivery.LeaseExpiresAt);
        Assert.Equal(0, fixture.Repository.SaveCalls);

        fixture.Clock.Now = fixture.Clock.Now.Add(fixture.Options.LeaseDuration).AddSeconds(1);
        fixture.Sender.Send = (_, _) => Task.FromResult(fixture.Result(SendOutcome.Success, 200));
        Assert.True(await fixture.Processor.ProcessNextAsync());
        Assert.Equal(DeliveryStatus.Delivered, fixture.Delivery.Status);
        Assert.Equal(1, Assert.Single(fixture.Delivery.Attempts).AttemptNumber);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedFailure_IsRetryableWithoutPersistingExceptionSecrets(bool decryptionFails)
    {
        var fixture = new Fixture();
        const string sensitiveMessage = "private-token-value";
        if (decryptionFails)
            fixture.Keys.DecryptError = new InvalidOperationException(sensitiveMessage);
        else
            fixture.Sender.Send = (_, _) => throw new InvalidOperationException(sensitiveMessage);

        Assert.True(await fixture.Processor.ProcessNextAsync());

        Assert.Equal(DeliveryStatus.RetryScheduled, fixture.Delivery.Status);
        var attempt = Assert.Single(fixture.Delivery.Attempts);
        Assert.False(attempt.Succeeded);
        Assert.Null(attempt.HttpStatusCode);
        Assert.False(string.IsNullOrWhiteSpace(attempt.ErrorMessage));
        Assert.DoesNotContain(sensitiveMessage, attempt.ErrorMessage!);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        if (decryptionFails)
            Assert.Empty(fixture.Sender.Requests);
    }

    [Fact]
    public async Task LostClaim_DoesNotRepeatTheSendOrRetryTheSave()
    {
        var fixture = new Fixture();
        fixture.Repository.SaveAccepted = false;

        Assert.True(await fixture.Processor.ProcessNextAsync());

        Assert.Single(fixture.Sender.Requests);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        Assert.Equal(fixture.Repository.LastIssuedLease, fixture.Repository.LastSavedLease);
    }

    private sealed class Fixture
    {
        public TestClock Clock { get; } = new();
        public DeliveryProcessingOptions Options { get; } = new();
        public StubSender Sender { get; } = new();
        public StubKeys Keys { get; } = new();
        public Delivery Delivery { get; }
        public StubRepository Repository { get; }
        public DeliveryProcessor Processor { get; }

        public Fixture(bool hasDelivery = true)
        {
            var project = Project.Create("Test", Guid.NewGuid()).Project;
            var endpoint = project.RegisterEndpoint("https://example.com/webhook", "encrypted-signing-secret", "Test");
            var evt = Event.Create(project.ProjectId, "order.created", "{ \"reference\": \"caf\u00e9\", \"amount\": 10.00 }");
            Delivery = Delivery.Create(evt.EventId, endpoint.EndpointId);
            Set(Delivery, nameof(Delivery.Event), evt);
            Set(Delivery, nameof(Delivery.Endpoint), endpoint);
            Set(evt, nameof(Event.Project), project);
            Set(endpoint, nameof(WebhookEndpoint.Project), project);
            Repository = new StubRepository(hasDelivery ? Delivery : null);
            Sender.Send = (_, _) => Task.FromResult(Result(SendOutcome.Success, 200));
            Processor = new DeliveryProcessor(Repository, Sender, Keys,
                Microsoft.Extensions.Options.Options.Create(Options), Clock, NullLogger<DeliveryProcessor>.Instance);
        }

        public WebhookSendResult Result(SendOutcome outcome, int? status) =>
            new(outcome, status, null, null, TimeSpan.FromMilliseconds(15), Clock.Now, null);
    }

    // An in-memory boundary stub: repository concurrency and persisted lease fencing
    // belong in relational integration tests, not this processor lifecycle suite.
    private sealed class StubRepository(Delivery? delivery) : IDeliveryRepository
    {
        public int SaveCalls { get; private set; }
        public bool SaveAccepted { get; set; } = true;
        public Guid? LastIssuedLease { get; private set; }
        public Guid? LastSavedLease { get; private set; }
        public DateTime? LastClaimAt { get; private set; }
        public TimeSpan? LastLeaseDuration { get; private set; }

        public Task<Delivery?> ClaimAsync(Guid deliveryId, DateTime now, TimeSpan leaseDuration, CancellationToken ct = default) =>
            delivery?.DeliveryId == deliveryId
                ? ClaimNextAsync(now, leaseDuration, ct)
                : Task.FromResult<Delivery?>(null);

        public Task<Delivery?> ClaimNextAsync(DateTime now, TimeSpan leaseDuration, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            LastClaimAt = now;
            LastLeaseDuration = leaseDuration;
            var isDue = delivery is not null && (delivery.Status == DeliveryStatus.Queued ||
                (delivery.Status == DeliveryStatus.RetryScheduled && delivery.NextRetryAt <= now) ||
                (delivery.Status == DeliveryStatus.Processing && delivery.LeaseExpiresAt <= now));
            if (!isDue)
                return Task.FromResult<Delivery?>(null);

            LastIssuedLease = Guid.NewGuid();
            Set(delivery!, nameof(Delivery.Status), DeliveryStatus.Processing);
            Set(delivery!, nameof(Delivery.LeaseToken), LastIssuedLease);
            Set(delivery!, nameof(Delivery.LeaseExpiresAt), now.Add(leaseDuration));
            return Task.FromResult(delivery);
        }

        public Task<bool> SaveClaimedAsync(Delivery claimedDelivery, Guid leaseToken, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            SaveCalls++;
            LastSavedLease = leaseToken;
            return Task.FromResult(SaveAccepted);
        }
    }

    private sealed class StubSender : IWebhookSender
    {
        public List<WebhookSendRequest> Requests { get; } = [];
        public Func<WebhookSendRequest, CancellationToken, Task<WebhookSendResult>> Send { get; set; } = null!;

        public Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Send(request, ct);
        }
    }

    private sealed class StubKeys : ISigningKeyService
    {
        public string? LastEncryptedValue { get; private set; }
        public Exception? DecryptError { get; set; }
        public string GenerateSigningKey() => throw new NotSupportedException();
        public string Encrypt(string signingKey) => throw new NotSupportedException();
        public string Decrypt(string encryptedSigningKey)
        {
            LastEncryptedValue = encryptedSigningKey;
            if (DecryptError is not null)
                throw DecryptError;
            return "plain-signing-secret";
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property)!.SetValue(instance, value);
}
