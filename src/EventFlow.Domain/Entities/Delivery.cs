namespace EventFlow.Domain.Entities;

public class Delivery
{
    public Guid DeliveryId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = new Event();
    public Guid EndpointId { get; set; }
    public Endpoint Endpoint { get; set; } = new Endpoint();
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; } = 0;
    public DateTime LastAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime NextRetryAt { get; set; } = DateTime.UtcNow;
    public int ResponseStatusCode { get; set; } = 0;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}