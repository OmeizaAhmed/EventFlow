namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
using EventFlow.Domain.Enums;
public class Project
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    private readonly List<EventSubscription> _subscriptions = new();
    public IReadOnlyCollection<EventSubscription> Subscriptions => _subscriptions.AsReadOnly();
    private readonly List<ProjectMembership> _projectMemberships = new();
    public IReadOnlyCollection<ProjectMembership> ProjectMemberships =>
        _projectMemberships.AsReadOnly();

    private readonly List<ApiKey> _apiKeys = new();
    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();

    private readonly List<WebhookEndpoint> _endpoints = new();
    public IReadOnlyCollection<WebhookEndpoint> Endpoints => _endpoints.AsReadOnly();

    private readonly List<Event> _events = new();
    public IReadOnlyCollection<Event> Events => _events.AsReadOnly();

    private Project() { } // EF Core

    public static ProjectCreationResponse Create(string name, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Project name is required");
    

        var newProject = new Project
        {
            ProjectId = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        // create project membership for the creator
        var membership = ProjectMembership.Create(userId: createdByUserId, projectId: newProject.ProjectId);
        return new ProjectCreationResponse(newProject, membership);
    }

    public Project UpdateProject(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Project name is required");
        Name = name;
        UpdatedAt = DateTime.UtcNow;
        return this;
    }
    public ApiKey GenerateApiKey(string hashedKey, string keyPrefix, bool isLive)
    {
        if (!IsActive)
            throw new ValidationException("Cannot generate an API key for an inactive project");

        var key = new ApiKey(ProjectId, hashedKey, keyPrefix, isLive);
        _apiKeys.Add(key);
        return key;
    }

    public ApiKey RevokeApiKey(Guid apiKeyId, DateTime? revokedAt = null)
    {
        var key = _apiKeys.FirstOrDefault(k => k.ApiKeyId == apiKeyId && k.ProjectId == ProjectId)
                   ?? throw new NotFoundException("Api-key", apiKeyId);
        key.Revoke(revokedAt);
        return key;
    }

    public WebhookEndpoint RegisterEndpoint(string url, string signingSecretValue, string description)
    {
        if (!IsActive)
            throw new ValidationException("Cannot register an endpoint on an inactive project");

        var endpoint = WebhookEndpoint.Create(ProjectId, url, new ValueObject.SigningSecret(signingSecretValue), description);
        _endpoints.Add(endpoint);
        return endpoint;
    }

  

    public void Deactivate() => IsActive = IsActive ? false : throw new ValidationException("Project is already inactive");

    public void Activate() => IsActive = IsActive ? throw new ValidationException("Project is already active") : true;

} 
public record ProjectCreationResponse(Project Project, ProjectMembership Membership);
