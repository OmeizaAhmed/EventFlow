using EventFlow.Domain.Interfaces;
using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EventFlow.Infrastructure.Persistence;

namespace EventFlow.Infrastructure.Repositories;
public class UserRepository : IUserRepository
{
    private readonly EventFlowDbContext _context;
    public UserRepository(EventFlowDbContext context)
    {
        _context = context;
    }

    public async Task<DomainUser> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.DomainUsers.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found.");
        }
        return user;
    }
    public async Task<DomainUser> GetUserByEmailAsync(string email)
    {
        var user = await _context.DomainUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with email {email} not found.");
        }
        return user;
    }
    public async Task AddUserAsync(DomainUser user)
    {
        await _context.DomainUsers.AddAsync(user);
        await _context.SaveChangesAsync();
    }
    public async Task UpdateUserAsync(DomainUser user)
    {
        _context.DomainUsers.Update(user);
        await _context.SaveChangesAsync();
    }
    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _context.DomainUsers.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found.");
        }
        _context.DomainUsers.Remove(user);
        await _context.SaveChangesAsync();
    }
}