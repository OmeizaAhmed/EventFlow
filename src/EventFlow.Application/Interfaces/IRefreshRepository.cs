using EventFlow.Domain.Entities;

namespace EventFlow.Application.Interfaces;
public interface IRefreshRepository
{
    void AddRefreshTokenAsync(RefreshToken refreshToken);
    Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);
    Task DeleteRefreshTokenAsync(string refreshToken);
    Task SaveChangesAsync();
}