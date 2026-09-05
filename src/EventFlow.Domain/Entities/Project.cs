namespace EventFlow.Domain.Entities;

using System.Collections.Generic;

public class Project
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public  string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public ICollection<Endpoint> Endpoints { get; set; } = new List<Endpoint>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<EndpointSubscription> EndpointSubscriptions { get; set; } = new List<EndpointSubscription>();
}