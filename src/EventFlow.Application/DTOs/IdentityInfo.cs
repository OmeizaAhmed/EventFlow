namespace EventFlow.Application.DTOs;

public class IdentityInfo
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
}