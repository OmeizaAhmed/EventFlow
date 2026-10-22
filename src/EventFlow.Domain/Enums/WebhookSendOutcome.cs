using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Domain.Enums
{
    public enum WebhookSendOutcome
    {
        /// <summary>2xx response.</summary>
        Success,
        /// <summary>Worth retrying: timeout, network error, 408, 429, 5xx.</summary>
        RetryableFailure,
        /// <summary>Retrying will not help: other 4xx, 3xx (redirects are not followed).</summary>
        PermanentFailure
    }
}
