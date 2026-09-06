namespace EventFlow.Infrastructure.Services;
using EventFlow.Application.Repository;
using EventFlow.Application.DTOs;
using EventFlow.Domain.ValueObject;
using Microsoft.AspNetCore.Identity;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Interfaces;

public class AuthService: IAuthRepository
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }
    public async Task<string> AuthenticateAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var userInfo = new UserInfo
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Organization = user.Organization,
            Roles = (await _userManager.GetRolesAsync(user)).ToList()
        };

        return _tokenService.GenerateToken(userInfo);
    }

    public async Task<bool> RegisterAsync(RegisterInput userInfo)
    {
        var user = new ApplicationUser
        {
            UserName = userInfo.Email,
            Email = userInfo.Email,
            FirstName = userInfo.FirstName,
            LastName = userInfo.LastName,
            Organization = userInfo.Organization
        };

        var result = await _userManager.CreateAsync(user, userInfo.Password);
        if (!result.Succeeded)
        {
            return false;
        }

        return true;
    }

    public Task<UserInfo> GetUserInfoAsync(string userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UpdateUserInfoAsync(UserInfo userInfo)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteUserAsync(string userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        throw new NotImplementedException();
    }
}