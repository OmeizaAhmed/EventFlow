namespace EventFlow.Domain.Entities;

public class Event
{
    public Guid EventId { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = new Project();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}