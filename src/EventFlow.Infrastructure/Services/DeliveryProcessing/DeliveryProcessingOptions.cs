namespace EventFlow.Infrastructure.Services;

public sealed class DeliveryProcessingOptions
{
    public const string SectionName = "DeliveryProcessing";

    public bool Enabled { get; set; } = true;
    public int WorkerCount { get; set; } = 4;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>Total completed attempts, including the initial send.</summary>
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Caps both exponential backoff and a receiver's Retry-After.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);
}
