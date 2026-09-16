namespace EventFlow.Domain.ValueObject;

public class UserInfo
{
    public Guid UserId { get; set; } = Guid.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid AuthId { get; set; } = Guid.Empty;
    public ICollection<string> Roles { get; set; } = new List<string>();
}