namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
public class ApiKey
{
    public Guid ApiKeyId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string HashedKey { get; private set; } = string.Empty;
    public string KeyPrefix { get; private set; } = string.Empty; // shown in dashboard, e.g. "ef_live_8a2f..."
    public bool IsLive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public bool IsRevoked => RevokedAt is not null;

    private ApiKey() { } // EF Core

    internal ApiKey(Guid projectId, string hashedKey, string keyPrefix, bool isLive)
    {
        ApiKeyId = Guid.NewGuid();
        ProjectId = projectId;
        HashedKey = hashedKey;
        KeyPrefix = keyPrefix;
        IsLive = isLive;
        CreatedAt = DateTime.UtcNow;
    }

    internal void Revoke()
    {
        if (IsRevoked) throw new DomainException("API key is already revoked");
        RevokedAt = DateTime.UtcNow;
    }
}