namespace EventFlow.Application.Interfaces;
using System.Threading.Tasks;
using EventFlow.Application.DTOs;
public interface IAuthService
{
    Task RegisterUserAsync(RegisterInput registerInput);
    Task<AuthOutput> LoginUserAsync(string email, string password);
    Task<AuthOutput> RefreshTokenAsync(string refreshToken);
}