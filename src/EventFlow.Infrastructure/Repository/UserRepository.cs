using EventFlow.Domain.Interfaces;
using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EventFlow.Infrastructure.Persistence;

namespace EventFlow.Infrastructure.Repository;
public class UserRepository : IUserRepository
{
    private readonly EventFlowDbContext _context;
    public UserRepository(EventFlowDbContext context)
    {
        _context = context;
    }

    public async Task<Administrator> GetAdministratorByIdAsync(Guid userId)
    {
        var administrator = await _context.Administrators.FirstOrDefaultAsync(a => a.AdministratorId == userId);
        if (administrator == null)
        {
            throw new KeyNotFoundException($"Administrator with ID {userId} not found.");
        }
        return administrator;
    }
    public async Task<Administrator> GetAdministratorByEmailAsync(string email)
    {
        var administrator = await _context.Administrators.FirstOrDefaultAsync(a => a.Email == email);
        if (administrator == null)
        {
            throw new KeyNotFoundException($"Administrator with email {email} not found.");
        }
        return administrator;
    }
    public async Task AddAdministratorAsync(Administrator administrator)
    {
        await _context.Administrators.AddAsync(administrator);
        await _context.SaveChangesAsync();
    }
    public async Task UpdateAdministratorAsync(Administrator administrator)
    {
        _context.Administrators.Update(administrator);
        await _context.SaveChangesAsync();
    }
    public async Task DeleteAdministratorAsync(Guid userId)
    {
        var administrator = await _context.Administrators.FirstOrDefaultAsync(a => a.AdministratorId == userId);
        if (administrator == null)
        {
            throw new KeyNotFoundException($"Administrator with ID {userId} not found.");
        }
        _context.Administrators.Remove(administrator);
        await _context.SaveChangesAsync();
    }
}