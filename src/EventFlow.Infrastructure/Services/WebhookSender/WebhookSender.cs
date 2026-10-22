using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;

namespace EventFlow.Infrastructure.Services;


public sealed class WebhookSender : IWebhookSender
{
    private readonly HttpClient _http;
    private readonly IWebhookSignatureGenerator _signer;
    private readonly WebhookSenderOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(
        HttpClient http,
        IWebhookSignatureGenerator signer,
        IOptions<WebhookSenderOptions> options,
        TimeProvider time,
        ILogger<WebhookSender> logger)
    {
        _http = http;
        _signer = signer;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken ct = default)
    {
        var attemptedAt = _time.GetUtcNow();
        var started = Stopwatch.GetTimestamp();
        TimeSpan Elapsed() => Stopwatch.GetElapsedTime(started);

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new WebhookSendResult(WebhookSendOutcome.PermanentFailure, null, null,
                "Endpoint URL is not a valid http(s) URL.", Elapsed(), attemptedAt, null);
        }

        var timestamp = attemptedAt.ToUnixTimeSeconds();
        var signature = _signer.Sign(request.Secret, timestamp, request.Payload);

        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(request.Payload, Encoding.UTF8, "application/json")
        };
        message.Headers.UserAgent.ParseAdd(_options.UserAgent);
        message.Headers.Add("EventFlow-Delivery-Id", request.DeliveryId.ToString());
        message.Headers.Add("EventFlow-Event-Id", request.EventId.ToString());
        message.Headers.Add("EventFlow-Event-Type", request.EventType);
        message.Headers.Add("EventFlow-Timestamp", timestamp.ToString());
        message.Headers.Add("EventFlow-Signature", $"t={timestamp},v1={signature}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.Timeout);

        try
        {
            using var response = await _http.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

            var body = await ReadTruncatedAsync(response.Content, timeoutCts.Token);
            var status = (int)response.StatusCode;
            var outcome = Classify(response.StatusCode);

            TimeSpan? retryAfter = null;
            var ra = response.Headers.RetryAfter;
            if (ra?.Delta is { } delta) retryAfter = delta;
            else if (ra?.Date is { } date) retryAfter = date - attemptedAt is { } d && d > TimeSpan.Zero ? d : null;

            _logger.LogInformation(
                "Webhook delivery {DeliveryId} to {Host} -> {Status} ({Outcome}) in {Ms}ms",
                request.DeliveryId, uri.Host, status, outcome, (int)Elapsed().TotalMilliseconds);

            return new WebhookSendResult(outcome, status, body,
                outcome == WebhookSendOutcome.Success ? null : $"Endpoint responded with HTTP {status}.",
                Elapsed(), attemptedAt, retryAfter);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our own timeout fired, not the caller cancelling.
            _logger.LogWarning("Webhook delivery {DeliveryId} to {Host} timed out after {Timeout}",
                request.DeliveryId, uri.Host, _options.Timeout);
            return new WebhookSendResult(WebhookSendOutcome.RetryableFailure, null, null,
                $"Request timed out after {_options.Timeout.TotalSeconds:0.#}s.", Elapsed(), attemptedAt, null);
        }
        catch (HttpRequestException ex)
        {
            // DNS failure, connection refused, TLS error, etc.
            _logger.LogWarning(ex, "Webhook delivery {DeliveryId} to {Host} failed to connect",
                request.DeliveryId, uri.Host);
            return new WebhookSendResult(WebhookSendOutcome.RetryableFailure, null, null,
                $"Connection error: {ex.Message}", Elapsed(), attemptedAt, null);
        }
    }

    private static WebhookSendOutcome Classify(HttpStatusCode code)
    {
        var status = (int)code;
        if (status is >= 200 and < 300) return WebhookSendOutcome.Success;
        if (status is 408 or 425 or 429 or >= 500) return WebhookSendOutcome.RetryableFailure;
        return WebhookSendOutcome.PermanentFailure; // 3xx (not followed) and other 4xx
    }

    private async Task<string> ReadTruncatedAsync(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        var buffer = new byte[_options.MaxResponseBodyBytes];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) break;
            total += read;
        }
        return Encoding.UTF8.GetString(buffer, 0, total);
    }
}



