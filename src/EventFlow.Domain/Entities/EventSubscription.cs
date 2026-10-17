using EventFlow.Domain.Exceptions;

namespace EventFlow.Domain.Entities;
public class EventSubscription
{
    public Guid EventSubscriptionId { get; private set; }
    public Guid EndpointId { get; private set; }
    public WebhookEndpoint Endpoint { get; private set; } = null!;
    public Guid ProjectId { get; private set; }
    public Project Project { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private EventSubscription() { } // EF Core

    internal EventSubscription(Guid endpointId, Guid projectId, string eventType)
    {
        EventSubscriptionId = Guid.NewGuid();
        EndpointId = endpointId;
        ProjectId = projectId;
        EventType = eventType.ToLowerInvariant().Trim();
        CreatedAt = DateTime.UtcNow;
    }

    // Update the event type of the subscription
    public void UpdateEventType(string newEventType)
    {
        if (string.IsNullOrWhiteSpace(newEventType))
            throw new ValidationException("Event type cannot be null or empty");
        EventType = newEventType.ToLowerInvariant().Trim();
    }
}