namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;

public class Delivery
{
    public Guid DeliveryId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid EndpointId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }

    private readonly List<DeliveryAttempt> _attempts = new();
    public IReadOnlyCollection<DeliveryAttempt> Attempts => _attempts.AsReadOnly();
    public int AttemptCount => _attempts.Count;

    private Delivery() { }

    public static Delivery Create(Guid eventId, Guid endpointId)
    {
        return new Delivery
        {
            DeliveryId = Guid.NewGuid(),
            EventId = eventId,
            EndpointId = endpointId,
            Status = DeliveryStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordAttempt(int? httpStatusCode, string? errorMessage, bool succeeded)
    {
        if (!CanTransitionTo(succeeded ? DeliveryStatus.Delivered : DeliveryStatus.Failed))
            throw new ValidationException($"Cannot record an attempt while Delivery is {Status}");

        _attempts.Add(new DeliveryAttempt(AttemptCount + 1, httpStatusCode, errorMessage, DateTime.UtcNow));
        Status = succeeded ? DeliveryStatus.Delivered : DeliveryStatus.Failed;
    }

    public void ScheduleRetry(DateTime retryAt)
    {
        if (Status != DeliveryStatus.Failed)
            throw new ValidationException("Can only schedule a retry from a Failed state");

        Status = DeliveryStatus.RetryScheduled;
        NextRetryAt = retryAt;
    }

    public void MarkDeadLetter()
    {
        if (Status != DeliveryStatus.Failed && Status != DeliveryStatus.RetryScheduled)
            throw new ValidationException($"Cannot dead-letter a Delivery in {Status} state");

        Status = DeliveryStatus.DeadLetter;
        NextRetryAt = null;
    }

    private bool CanTransitionTo(DeliveryStatus newStatus)
    {
        // Implement your state transition logic here
        return Status switch
        {
            DeliveryStatus.Queued => newStatus == DeliveryStatus.Delivered || newStatus == DeliveryStatus.Failed,
            DeliveryStatus.Failed => newStatus == DeliveryStatus.RetryScheduled || newStatus == DeliveryStatus.DeadLetter,
            DeliveryStatus.RetryScheduled => newStatus == DeliveryStatus.Delivered || newStatus == DeliveryStatus.Failed || newStatus == DeliveryStatus.DeadLetter,
            DeliveryStatus.Delivered => false,
            DeliveryStatus.DeadLetter => false,
            _ => false
        };
    }
}