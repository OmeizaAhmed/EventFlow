namespace EventFlow.Application.Repository;
using EventFlow.Domain.ValueObject;
using EventFlow.Application.DTOs;

public interface IAuthRepository
{
    Task<string> AuthenticateAsync(string email, string password);
    Task<bool> RegisterAsync(RegisterInput userInfo);
    Task<UserInfo> GetUserInfoAsync(string userId);
    Task<bool> UpdateUserInfoAsync(UserInfo userInfo);
    Task<bool> DeleteUserAsync(string userId);
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    
}