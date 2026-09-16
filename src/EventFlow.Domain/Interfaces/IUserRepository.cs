namespace EventFlow.Domain.Interfaces;
using EventFlow.Domain.Entities;
public interface IUserRepository
{
    Task<DomainUser> GetUserByIdAsync(Guid userId);
    Task<DomainUser> GetUserByEmailAsync(string email);
    Task AddUserAsync(DomainUser user);
    Task UpdateUserAsync(DomainUser user);
    Task DeleteUserAsync(Guid userId);
}