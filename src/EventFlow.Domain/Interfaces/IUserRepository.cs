namespace EventFlow.Domain.Interfaces;
using EventFlow.Domain.Entities;
using System;
using System.Threading.Tasks;
public interface IUserRepository
{
    Task<DomainUser> GetUserByIdAsync(Guid userId);
    Task<DomainUser> GetUserByEmailAsync(string email);
    Task<DomainUser> GetUserByAuthIdAsync(Guid authId);
    Task AddUserAsync(DomainUser user);
    Task UpdateUserAsync(DomainUser user);
    Task DeleteUserAsync(Guid userId);
}