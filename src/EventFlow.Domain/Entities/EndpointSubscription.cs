namespace EventFlow.Domain.Entities;
public class EndpointSubscription
{
    public Guid EndpointSubscriptionId { get; set; }
    public Guid EndpointId { get; set; }
    public Endpoint Endpoint { get; set; } = new Endpoint();
    public string EventType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}