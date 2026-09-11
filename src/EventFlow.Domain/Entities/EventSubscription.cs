namespace EventFlow.Domain.Entities;
public class EventSubscription
{
    public Guid EventSubscriptionId { get; private set; }
    public Guid EndpointId { get; private set; }
    public string EventType { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private EventSubscription() { } // EF Core

    internal EventSubscription(Guid endpointId, string eventType)
    {
        EventSubscriptionId = Guid.NewGuid();
        EndpointId = endpointId;
        EventType = eventType;
        CreatedAt = DateTime.UtcNow;
    }
}