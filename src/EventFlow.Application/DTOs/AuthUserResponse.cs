namespace EventFlow.Application.DTOs;

public record AuthUserResponse(Guid UserId, string Email, string FirstName, string LastName,
    IReadOnlyList<string> Roles);
