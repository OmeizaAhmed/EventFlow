using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.Interfaces
{
    public interface IWebhookSignatureGenerator
    {
        /// <summary>Returns the lowercase hex HMAC-SHA256 of "{timestamp}.{payload}".</summary>
        string Sign(string secret, long timestamp, string payload);
    }
}
