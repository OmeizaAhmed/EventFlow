namespace EventFlow.Application.Services;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Exceptions;

public class AuthService : IAuthService
{
    private readonly IIdentityRepository _identityRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshRepository _refreshRepository;

    public AuthService(IIdentityRepository identityRepository, IUserRepository userRepository, ITokenService tokenService, IRefreshRepository refreshRepository)
    {
        _identityRepository = identityRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _refreshRepository = refreshRepository;
    }

    public async Task RegisterUserAsync(RegisterInput registerInput)
    {
        // regex to validate password is (?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}
        if (!System.Text.RegularExpressions.Regex.IsMatch(registerInput.Password, @"(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}"))
        {
            throw new ValidationException("Password must be at least 8 characters long and contain both letters and numbers");
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
    } 

    public async Task<string> LoginUserAsync(string email, string password)
    {
        var isPasswordValid = await _identityRepository.CheckPasswordAsync(email, password);
        if (!isPasswordValid)
        {
            throw new ValidationException("Invalid email or password");
        }

        var user = await _userRepository.GetUserByEmailAsync(email);
        var userInfo = new UserInfo
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AuthId = user.AuthId,
            Roles = await _identityRepository.GetIdentityRolesAsync(user.AuthId)
        };
        // create refresh token for the user
        var refreshToken = RefreshToken.Create(userInfo.UserId, Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(7));
        await AddRefreshTokenAsync(refreshToken);
        
        return _tokenService.GenerateToken(userInfo);
    }

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken)
    {
        _refreshRepository.AddRefreshTokenAsync(refreshToken);
        await _refreshRepository.SaveChangesAsync();
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken)
    {
        return await _refreshRepository.GetRefreshTokenAsync(refreshToken);
    }

    public async Task DeleteRefreshTokenAsync(string refreshToken)
    {
        await _refreshRepository.DeleteRefreshTokenAsync(refreshToken);
        await _refreshRepository.SaveChangesAsync();
    }
}