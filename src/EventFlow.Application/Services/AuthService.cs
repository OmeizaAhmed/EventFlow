namespace EventFlow.Application.Services;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class AuthService : IAuthService
{
    private readonly IIdentityRepository _identityRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshRepository _refreshRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IIdentityRepository identityRepository, IUserRepository userRepository, ITokenService tokenService, IRefreshRepository refreshRepository, ILogger<AuthService>? logger = null)
    {
        _identityRepository = identityRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _refreshRepository = refreshRepository;
        _logger = logger ?? NullLogger<AuthService>.Instance;
    }

    public async Task RegisterUserAsync(RegisterInput registerInput)
    {
        // regex to validate password is numeric and alphabetic and symbol and at least 8 characters long
        if (!System.Text.RegularExpressions.Regex.IsMatch(registerInput.Password, @"(?=.*[A-Za-z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}"))
        {
            throw new ValidationException("Password must be at least 8 characters long and contain letters, numbers, and symbols");
        }
        // check if email already exists
        var existingIdentity = await _identityRepository.GetIdentityByEmailAsync(registerInput.Email);
        if (existingIdentity != null)
        {
            throw new ConflictException($"Email '{registerInput.Email}' is already registered");
        }
        // add user to identity repository
        var identity= await _identityRepository.CreateIdentityAsync(registerInput.Email, registerInput.Password);
        // add user to domain repository
        var User = DomainUser.Create(registerInput.Email, registerInput.FirstName, registerInput.LastName, identity.Id);
        await _userRepository.AddUserAsync(User);
        _logger.LogInformation("Registered user {UserId}", User.UserId);
    } 

    public async Task<AuthOutput> LoginUserAsync(string email, string password)
    {
        var isPasswordValid = await _identityRepository.CheckPasswordAsync(email, password);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Login rejected due to invalid credentials");
            throw new ValidationException("Invalid email or password");
        }

        var user = await _userRepository.GetUserByEmailAsync(email);
        var userInfo = await GetUserInfoAsync(user);
        var refreshToken = RefreshToken.Create(userInfo.UserId, Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(7));
        await _refreshRepository.AddRefreshTokenAsync(refreshToken);
        _logger.LogInformation("User {UserId} signed in", userInfo.UserId);
        
        
       string accessToken = _tokenService.GenerateToken(userInfo);
        return new AuthOutput(accessToken, refreshToken.Token); 
    }

    public async Task<AuthOutput?> RefreshTokenAsync(string refreshToken)
    {
        var existingRefreshToken = await _refreshRepository.GetRefreshTokenAsync(refreshToken);
        if (existingRefreshToken is null || existingRefreshToken.ExpiresAt <= DateTime.UtcNow ||
            existingRefreshToken.RevokedAt is not null)
        {
            _logger.LogWarning("Refresh token rejected");
            return null;
        }

        var user = await _userRepository.GetUserByIdAsync(existingRefreshToken.UserId);
        var userInfo = await GetUserInfoAsync(user);
        var newRefreshToken = RefreshToken.Create(userInfo.UserId, Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(7),
            existingRefreshToken.SessionId ?? existingRefreshToken.Id);
        var accessToken = _tokenService.GenerateToken(userInfo);
        if (!await _refreshRepository.RotateRefreshTokenAsync(existingRefreshToken, newRefreshToken))
            return null;

        _logger.LogInformation("User {UserId} refreshed authentication", userInfo.UserId);
        return new AuthOutput(accessToken, newRefreshToken.Token);
    }

    public Task LogoutAsync(string refreshToken) => _refreshRepository.RevokeRefreshSessionAsync(refreshToken);

    public async Task<AuthUserResponse> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        var profile = await GetUserInfoAsync(user);
        return new(profile.UserId, profile.Email, profile.FirstName, profile.LastName, profile.Roles.ToArray());
    }

    private async Task<UserInfo> GetUserInfoAsync(DomainUser user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        AuthId = user.AuthId,
        Roles = await _identityRepository.GetIdentityRolesAsync(user.AuthId)
    };
}
