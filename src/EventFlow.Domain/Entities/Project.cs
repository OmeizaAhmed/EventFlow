namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
using EventFlow.Domain.Enums;
public class Project
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<ApiKey> _apiKeys = new();
    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();

    private readonly List<WebhookEndpoint> _endpoints = new();
    public IReadOnlyCollection<WebhookEndpoint> Endpoints => _endpoints.AsReadOnly();

    private Project() { } // EF Core

    public static Project Create(string name, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Project name is required");
    

        var newProject = new Project
        {
            ProjectId = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        // create project membership for the creator
        var membership = ProjectMembership.Invite(
            createdByUserId,
            newProject.ProjectId,
            ProjectMembershipRole.Owner,
            createdByUserId
        );

        return newProject;
    }

    public ApiKey GenerateApiKey(string hashedKey, string keyPrefix, bool isLive)
    {
        if (!IsActive)
            throw new ValidationException("Cannot generate an API key for an inactive project");

        var key = new ApiKey(ProjectId, hashedKey, keyPrefix, isLive);
        _apiKeys.Add(key);
        return key;
    }

    public void RevokeApiKey(Guid apiKeyId)
    {
        var key = _apiKeys.FirstOrDefault(k => k.ApiKeyId == apiKeyId)
                   ?? throw new NotFoundException("Api-key", apiKeyId);
        key.Revoke();
    }

    public WebhookEndpoint RegisterEndpoint(string url, string signingSecretValue)
    {
        if (!IsActive)
            throw new ValidationException("Cannot register an endpoint on an inactive project");

        var endpoint = WebhookEndpoint.Create(ProjectId, url, new ValueObject.SigningSecret(signingSecretValue));
        _endpoints.Add(endpoint);
        return endpoint;
    }

    public void Deactivate() => IsActive = false;
}