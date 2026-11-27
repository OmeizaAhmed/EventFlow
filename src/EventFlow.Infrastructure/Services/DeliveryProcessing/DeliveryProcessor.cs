using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventFlow.Infrastructure.Services;

public sealed class DeliveryProcessor(
    IDeliveryRepository repository,
    IWebhookSender sender,
    ISigningKeyService signingKeys,
    IOptions<DeliveryProcessingOptions> options,
    TimeProvider time,
    ILogger<DeliveryProcessor> logger) : IDeliveryProcessor
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var delivery = await repository.ClaimNextAsync(
            time.GetUtcNow().UtcDateTime, settings.LeaseDuration, cancellationToken);
        if (delivery is null) return false;

        await SendClaimedAsync(delivery, settings, cancellationToken);
        return true;
    }

    public async Task<DateTime?> ProcessAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var delivery = await repository.ClaimAsync(
            deliveryId, time.GetUtcNow().UtcDateTime, settings.LeaseDuration, cancellationToken);

        return delivery is null ? null : await SendClaimedAsync(delivery, settings, cancellationToken);
    }

    private async Task<DateTime?> SendClaimedAsync(Delivery delivery, DeliveryProcessingOptions settings,
        CancellationToken cancellationToken)
    {
        var leaseToken = delivery.LeaseToken
            ?? throw new InvalidOperationException("A claimed delivery must have a lease token.");

        // Leave time to persist the outcome before another worker can reclaim it.
        // The database lease token remains the authority if the process stalls.
        var remaining = delivery.LeaseExpiresAt!.Value - time.GetUtcNow().UtcDateTime
            - TimeSpan.FromSeconds(5);
        if (remaining <= TimeSpan.Zero)
        {
            logger.LogWarning("Delivery {DeliveryId} lease elapsed before sending", delivery.DeliveryId);
            return null;
        }

        using var deadline = new CancellationTokenSource(remaining, time);
        using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token);

        WebhookSendResult result;
        try
        {
            attemptCancellation.Token.ThrowIfCancellationRequested();
            var secret = signingKeys.Decrypt(delivery.Endpoint.SigningSecret.Value);
            result = await sender.SendAsync(new WebhookSendRequest(
                delivery.DeliveryId,
                delivery.EventId,
                delivery.Event.EventType,
                delivery.Endpoint.Url,
                delivery.Event.Payload,
                secret), attemptCancellation.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Leave the claim in place: its expiry makes interrupted work available again.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Delivery {DeliveryId} processing failed", delivery.DeliveryId);
            result = new WebhookSendResult(
                WebhookSendOutcome.RetryableFailure, null, null,
                "Delivery processing failed before an outcome could be recorded.",
                TimeSpan.Zero, time.GetUtcNow(), null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = result.Outcome == WebhookSendOutcome.Success;
        delivery.RecordAttempt(result.StatusCode, result.ErrorMessage, succeeded,
            result.AttemptedAt.UtcDateTime, result.ResponseBody,
            Math.Max(0, (long)result.Duration.TotalMilliseconds));

        if (!succeeded)
        {
            if (result.Outcome == WebhookSendOutcome.PermanentFailure ||
                delivery.AttemptCount >= settings.MaxAttempts)
            {
                delivery.MarkDeadLetter();
            }
            else
            {
                delivery.ScheduleRetry(time.GetUtcNow().UtcDateTime +
                    RetryDelay(delivery.AttemptCount, result.RetryAfter, settings));
            }
        }

        if (!await repository.SaveClaimedAsync(delivery, leaseToken, cancellationToken))
        {
            logger.LogWarning("Delivery {DeliveryId} outcome discarded because its lease was replaced",
                delivery.DeliveryId);
            return null;
        }

        logger.LogInformation("Delivery {DeliveryId} attempt {AttemptNumber} finished as {Status}; next retry {NextRetryAt}",
            delivery.DeliveryId, delivery.AttemptCount, delivery.Status, delivery.NextRetryAt);
        return delivery.NextRetryAt;
    }

    private static TimeSpan RetryDelay(int attemptCount, TimeSpan? retryAfter, DeliveryProcessingOptions settings)
    {
        var exponential = settings.InitialRetryDelay.TotalMilliseconds *
            Math.Pow(2, Math.Min(attemptCount - 1, 30));
        var cap = Math.Min(exponential, settings.MaxRetryDelay.TotalMilliseconds);
        var milliseconds = cap * (0.5 + Random.Shared.NextDouble() * 0.5);
        if (retryAfter is { } requested)
            milliseconds = Math.Max(milliseconds, requested.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, settings.MaxRetryDelay.TotalMilliseconds));
    }
}
