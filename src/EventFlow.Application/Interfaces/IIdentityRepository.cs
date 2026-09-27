namespace EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;

public interface IIdentityRepository
{
    Task<IdentityInfo> CreateIdentityAsync(string email, string password);
    Task<bool> CheckPasswordAsync(string username, string password);
    Task DeleteIdentityAsync(Guid id);
    Task UpdateIdentityEmailAsync(Guid id, string email);
    Task CreateRoleAsync(string roleName);
    Task DeleteRoleAsync(string roleName);
    Task<ICollection<string>> GetIdentityRolesAsync(Guid userId);
    Task<IdentityInfo?> GetIdentityByEmailAsync(string email);
    
}
