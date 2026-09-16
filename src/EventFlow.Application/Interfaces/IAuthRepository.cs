namespace EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;

public interface IAuthRepository
{
    public Task<string> LoginAsync(string email, string password);
    public Task LogoutAsync(string authId);
    public Task RegisterAsync(RegisterInput registerRequest);
    public Task ChangePasswordAsync(string authId, string oldPassword, string newPassword);
    public Task DeleteAccountAsync(string authId);
    public Task ResetPasswordAsync(string authId);
    public Task RefreshTokenAsync(string authId, string refreshToken);
}