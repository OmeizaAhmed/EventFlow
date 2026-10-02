namespace EventFlow.Infrastructure.Repositories;

using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using System.Threading.Tasks;

public class RefreshRepository : IRefreshRepository
{
    private readonly EventFlowDbContext _context;

    public RefreshRepository(EventFlowDbContext context)
    {
        _context = context;
    }
    public void AddRefreshTokenAsync(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Add(refreshToken);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken)
    {
        return await _context.RefreshTokens.FindAsync(refreshToken);
    }

    public async Task DeleteRefreshTokenAsync(string refreshToken)
    {
        var token = await _context.RefreshTokens.FindAsync(refreshToken);
        if (token != null)
        {
            _context.RefreshTokens.Remove(token);
        }
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}