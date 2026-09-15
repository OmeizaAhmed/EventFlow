namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;

public class User
{
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public Guid AuthId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { } // EF Core

    public static User Create(string email, Guid authId)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid email is required");
        if (authId == Guid.Empty)
            throw new DomainException("A valid AuthId is required");

        return new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            AuthId = authId,
            CreatedAt = DateTime.UtcNow
        };
    }
}