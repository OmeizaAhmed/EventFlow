namespace EventFlow.Application.Services;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using EventFlow.Application.DTOs;


public class AuthService
{
    private readonly IIdentityRepository _identityRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public AuthService(IIdentityRepository identityRepository, IUserRepository userRepository, ITokenService tokenService)
    {
        _identityRepository = identityRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task RegisterUserAsync(RegisterInput registerInput)
    {
        // add user to identity repository
        var identityId = await _identityRepository.CreateIdentityAsync(registerInput.Email, registerInput.Password);
        // add user to domain repository
        var User = DomainUser.Create(registerInput.Email, registerInput.FirstName, registerInput.LastName, identityId);
        await _userRepository.AddUserAsync(User);
    }

    public async Task<string> LoginUserAsync(string email, string password)
    {
        var isPasswordValid = await _identityRepository.CheckPasswordAsync(email, password);
        if (!isPasswordValid)
        {
            throw new Exception("Invalid email or password");
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
        return _tokenService.GenerateToken(userInfo);
    }

    
}