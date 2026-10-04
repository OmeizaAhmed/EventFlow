namespace EventFlow.Infrastructure.Repositories;

using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

public class RefreshRepository : IRefreshRepository
{
    private readonly EventFlowDbContext _context;

    public RefreshRepository(EventFlowDbContext context)
    {
        _context = context;
    }
    public async Task AddRefreshTokenAsync(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken)
    {
        var token = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken);
        return token;
    }

    public async Task DeleteRefreshTokenAsync(string refreshToken)
    {
        var token = await _context.RefreshTokens.FindAsync(refreshToken);
        if (token != null)
        {
            _context.RefreshTokens.Remove(token);
        }
        await _context.SaveChangesAsync();
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}