namespace EventFlow.Domain.Entities;
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastUsedAt { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, DateTime expiresAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or whitespace.", nameof(token));
        }

        if (expiresAt <= DateTime.UtcNow)
        {
            throw new ArgumentException("ExpiresAt must be in the future.", nameof(expiresAt));
        }
       return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };
    }

    public void Revoke()
    {
        if (RevokedAt.HasValue)
        {
            throw new InvalidOperationException("Refresh token is already revoked.");
        }

        RevokedAt = DateTime.UtcNow;
    }

    public void Use()
    {
        if (RevokedAt.HasValue)
        {
            throw new InvalidOperationException("Cannot use a revoked refresh token.");
        }
        if (ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Cannot use an expired refresh token.");
        }
    
        LastUsedAt = DateTime.UtcNow;
        RevokedAt = DateTime.UtcNow; // Token can be used only once
    }
}