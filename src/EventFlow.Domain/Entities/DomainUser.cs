namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Exceptions;
using EventFlow.Domain.ValueObject;

public class DomainUser
{
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Guid AuthId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private DomainUser() { } // EF Core

    public static DomainUser Create(string email, string firstName, string lastName, Guid authId)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ValidationException("A valid email is required");
        if (authId == Guid.Empty)
            throw new ValidationException("A valid AuthId is required");
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ValidationException("A valid first name is required");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ValidationException("A valid last name is required");

        return new DomainUser
        {
            UserId = Guid.NewGuid(),
            Email = email,
            AuthId = authId,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow
        };
    }
}