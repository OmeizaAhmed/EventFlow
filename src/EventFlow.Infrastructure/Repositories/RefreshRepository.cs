using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EventFlow.Infrastructure.Repositories;
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
        return await _context.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(rt => rt.Token == refreshToken);
    }

    public async Task<bool> RotateRefreshTokenAsync(RefreshToken current, RefreshToken replacement)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await LockUserAsync(current.UserId);

        var now = DateTime.UtcNow;
        var consumed = await _context.RefreshTokens
            .Where(token => token.Id == current.Id && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.LastUsedAt, now)
                .SetProperty(token => token.RevokedAt, (DateTime?)now));
        if (consumed == 0)
            return false;

        _context.RefreshTokens.Add(replacement);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task RevokeRefreshSessionAsync(string refreshToken)
    {
        var presented = await GetRefreshTokenAsync(refreshToken);
        if (presented is null)
            return;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await LockUserAsync(presented.UserId);
        var now = DateTime.UtcNow;
        if (presented.ExpiresAt > now)
        {
            var sessionId = presented.SessionId ?? presented.Id;
            await _context.RefreshTokens.Where(token => token.UserId == presented.UserId &&
                    (token.SessionId == sessionId || token.Id == sessionId) && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, (DateTime?)now));
        }
        await transaction.CommitAsync();
    }

    // Rotation and logout for the same user share this lock, including legacy tokens without a session ID.
    private Task<int> LockUserAsync(Guid userId) =>
        _context.DomainUsers.Where(user => user.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.CreatedAt, user => user.CreatedAt));
}
