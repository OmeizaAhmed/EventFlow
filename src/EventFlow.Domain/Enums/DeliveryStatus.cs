namespace EventFlow.Domain.Enums;

public enum DeliveryStatus
{
    Queued,
    Delivered,
    Failed,
    RetryScheduled,
    DeadLetter
}