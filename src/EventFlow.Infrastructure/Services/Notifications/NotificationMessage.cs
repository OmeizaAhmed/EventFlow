namespace EventFlow.Infrastructure.Services;

internal sealed class NotificationMessage
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTime AvailableAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? FailedAt { get; set; }
}
