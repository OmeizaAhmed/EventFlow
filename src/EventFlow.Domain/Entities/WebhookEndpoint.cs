namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
using EventFlow.Domain.ValueObject;
public class WebhookEndpoint
{
    public Guid EndpointId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public SigningSecret SigningSecret { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<EventSubscription> _subscriptions = new();
    public IReadOnlyCollection<EventSubscription> Subscriptions => _subscriptions.AsReadOnly();

    private WebhookEndpoint() { } // EF Core

    internal static WebhookEndpoint Create(Guid projectId, string url, SigningSecret signingSecret)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != "https")
            throw new DomainException("Webhook endpoint URL must be a valid HTTPS URL");

        return new WebhookEndpoint
        {
            EndpointId = Guid.NewGuid(),
            ProjectId = projectId,
            Url = url,
            SigningSecret = signingSecret,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public EventSubscription Subscribe(string eventType)
    {
        if (!IsActive)
            throw new DomainException("Cannot subscribe an inactive endpoint");

        if (_subscriptions.Any(s => s.EventType == eventType))
            throw new DomainException($"Endpoint is already subscribed to {eventType}");

        var subscription = new EventSubscription(EndpointId, eventType);
        _subscriptions.Add(subscription);
        return subscription;
    }

    public void Unsubscribe(string eventType)
    {
        var sub = _subscriptions.FirstOrDefault(s => s.EventType == eventType)
                   ?? throw new DomainException($"Endpoint is not subscribed to {eventType}");
        _subscriptions.Remove(sub);
    }

    public void Deactivate() => IsActive = false;
}