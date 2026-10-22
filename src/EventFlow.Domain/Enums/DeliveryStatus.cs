namespace EventFlow.Domain.Enums;

public enum DeliveryStatus
{
    Queued,
    Processing,
    Delivered,
    Failed,
    RetryScheduled,
    DeadLetter
}