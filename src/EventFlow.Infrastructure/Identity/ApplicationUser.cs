namespace EventFlow.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;   
using EventFlow.Domain;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}