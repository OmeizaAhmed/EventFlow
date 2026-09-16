namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
using EventFlow.Domain.ValueObject;
public class Event
{
    public Guid EventId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Payload { get; private set; } = string.Empty; // raw JSON
    public string? IdempotencyKey { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Event() { } // EF Core

    public static Event Create(Guid projectId, string eventType, string payload, string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ValidationException("Event payload cannot be empty");

        var evt = new Event
        {
            EventId = Guid.NewGuid(),
            ProjectId = projectId,
            EventType = eventType,
            Payload = payload,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        // Raised, not handled here — a listener in Application reacts by
        // triggering the Delivery Scheduler fan-out. Event itself doesn't
        // know that scheduling, Hangfire, or fan-out exist.
        // evt.RaiseDomainEvent(new EventPublishedDomainEvent(evt.EventId, evt.ProjectId));
        return evt;
    }
}