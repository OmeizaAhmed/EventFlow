using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.DTOs
{
    public sealed record WebhookSendRequest(
    Guid DeliveryId,
    Guid EventId,
    string EventType,
    string Url,
    string Payload,   // exact JSON string; the signature is computed over these bytes
    string Secret);   // the endpoint's signing secret (already decrypted by the caller)

    public enum WebhookSendOutcome
    {
        /// <summary>2xx response.</summary>
        Success,
        /// <summary>Worth retrying: timeout, network error, 408, 429, 5xx.</summary>
        RetryableFailure,
        /// <summary>Retrying will not help: other 4xx, 3xx (redirects are not followed).</summary>
        PermanentFailure
    }

    /// <summary>Result of one attempt. Maps directly onto a DeliveryAttempt row.</summary>
    public sealed record WebhookSendResult(
        WebhookSendOutcome Outcome,
        int? StatusCode,
        string? ResponseBody,
        string? ErrorMessage,
        TimeSpan Duration,
        DateTimeOffset AttemptedAt,
        TimeSpan? RetryAfter);
}
