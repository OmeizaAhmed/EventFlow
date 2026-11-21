namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;

public class Delivery
{
    public Guid DeliveryId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid? ReplayOfDeliveryId { get; private set; }
    public Event Event { get; private set; } = null!;
    public Guid EndpointId { get; private set; }
    public WebhookEndpoint Endpoint { get; private set; } = null!;
    public DeliveryStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }

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

    public Delivery Replay()
    {
        if (Status is not (DeliveryStatus.Delivered or DeliveryStatus.Failed or DeliveryStatus.DeadLetter))
            throw new ConflictException("Only delivered, failed, or dead-letter deliveries can be replayed.");
        var replay = Create(EventId, EndpointId);
        replay.ReplayOfDeliveryId = DeliveryId;
        return replay;
    }

    public void RecordAttempt(int? httpStatusCode, string? errorMessage, bool succeeded)
        => RecordAttempt(httpStatusCode, errorMessage, succeeded, DateTime.UtcNow, null, 0);

    public void RecordAttempt(
        int? httpStatusCode,
        string? errorMessage,
        bool succeeded,
        DateTime attemptedAt,
        string? responseBody,
        long durationMilliseconds)
    {
        if (!CanTransitionTo(succeeded ? DeliveryStatus.Delivered : DeliveryStatus.Failed))
            throw new ValidationException($"Cannot record an attempt while Delivery is {Status}");

        if (durationMilliseconds < 0)
            throw new ValidationException("Attempt duration cannot be negative");

        _attempts.Add(new DeliveryAttempt(
            DeliveryId, AttemptCount + 1, httpStatusCode, errorMessage, succeeded,
            attemptedAt, responseBody, durationMilliseconds));
        Status = succeeded ? DeliveryStatus.Delivered : DeliveryStatus.Failed;
        NextRetryAt = null;
        LeaseToken = null;
        LeaseExpiresAt = null;
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
        return (Status, newStatus) switch
            {
            (DeliveryStatus.Processing, DeliveryStatus.Delivered or DeliveryStatus.Failed) => true,
            (DeliveryStatus.Queued, DeliveryStatus.Processing or DeliveryStatus.Delivered or DeliveryStatus.Failed) => true,
            (DeliveryStatus.Failed, DeliveryStatus.RetryScheduled or DeliveryStatus.DeadLetter) => true,
            (DeliveryStatus.RetryScheduled, DeliveryStatus.Processing or DeliveryStatus.Delivered or DeliveryStatus.Failed or DeliveryStatus.DeadLetter) => true,
            _ => false
        };
    }
}
