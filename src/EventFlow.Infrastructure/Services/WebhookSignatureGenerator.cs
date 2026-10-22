using EventFlow.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace EventFlow.Infrastructure.Services
{
    public sealed class WebhookSignatureGenerator : IWebhookSignatureGenerator
    {
        public string Sign(string secret, long timestamp, string payload)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var data = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");
            return Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLowerInvariant();
        }
    }
}
