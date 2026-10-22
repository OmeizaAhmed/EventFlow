namespace EventFlow.Infrastructure.Services
{
    public sealed class WebhookSenderOptions
    {
        public const string SectionName = "WebhookSender";

        /// <summary>Hard cap for a single attempt (connect + send + read response).</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>Maximum response bytes retained for debugging; zero disables body capture.</summary>
        public int MaxResponseBodyBytes { get; set; } = 4096;

        public string UserAgent { get; set; } = "EventFlow-Webhooks/1.0";
    }
}
