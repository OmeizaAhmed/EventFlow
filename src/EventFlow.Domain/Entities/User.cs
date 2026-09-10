namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;

public class User
{
    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }   // simple model: one user, one project
    public string Email { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private User() { } // EF Core

    public static User Create(Guid projectId, string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid email is required");

        return new User
        {
            UserId = Guid.NewGuid(),
            ProjectId = projectId,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
    }
}