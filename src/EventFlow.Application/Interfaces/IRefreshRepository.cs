using EventFlow.Domain.Entities;

namespace EventFlow.Application.Interfaces;
public interface IRefreshRepository
{
    Task AddRefreshTokenAsync(RefreshToken refreshToken);
    Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);
    Task DeleteRefreshTokenAsync(string refreshToken);
    Task SaveChangesAsync();
}