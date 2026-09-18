namespace EventFlow.Application.Interfaces;


public interface IIdentityRepository
{
    Task<Guid> CreateIdentityAsync(string email, string password);
    Task<bool> CheckPasswordAsync(string username, string password);
    Task DeleteIdentityAsync(Guid id);
    Task UpdateIdentityEmailAsync(Guid id, string email);
    Task CreateRoleAsync(string roleName);
    Task DeleteRoleAsync(string roleName);
    Task<ICollection<string>> GetIdentityRolesAsync(Guid userId);
}
