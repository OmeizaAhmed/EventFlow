namespace EventFlow.Domain.Entities;
public class Endpoint
{
    public Guid EndpointId { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = new Project();
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<EndpointSubscription> EndpointSubscriptions { get; set; } = new List<EndpointSubscription>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}