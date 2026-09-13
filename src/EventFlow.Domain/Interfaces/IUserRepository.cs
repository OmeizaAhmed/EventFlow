namespace EventFlow.Domain.Interfaces;
using EventFlow.Domain.Entities;
public interface IUserRepository
{
    Task<Administrator> GetAdministratorByIdAsync(Guid administratorId);
    Task<Administrator> GetAdministratorByEmailAsync(string email);
    Task AddAdministratorAsync(Administrator administrator);
    Task UpdateAdministratorAsync(Administrator administrator);
    Task DeleteAdministratorAsync(Guid administratorId);
}