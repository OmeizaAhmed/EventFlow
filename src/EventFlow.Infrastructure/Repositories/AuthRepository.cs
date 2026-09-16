namespace EventFlow.Infrastructure.Repositories;
using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Interfaces;
using EventFlow.Domain.Exceptions;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Entities;
using EventFlow.Domain.ValueObject;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EventFlow.Domain.Interfaces;

public class AuthRepository : IAuthRepository
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenProvider;
    private readonly EventFlowDbContext _dbContext;
    private readonly IUserRepository _userRepository; 

    public AuthRepository(UserManager<ApplicationUser> userManager, ITokenService tokenProvider, EventFlowDbContext dbContext, IUserRepository userRepository)
    {
        _userManager = userManager;
        _tokenProvider = tokenProvider;
        _dbContext = dbContext;
        _userRepository = userRepository;
    }

    public async Task RegisterAsync(RegisterInput registerRequest)
    {
        // validate the register request
       if (registerRequest == null)
       {
           throw new BadRequestException(nameof(registerRequest));
       }
       ValidateInput(registerRequest.Email, nameof(registerRequest.Email));
       ValidateInput(registerRequest.Password, nameof(registerRequest.Password));
       ValidateInput(registerRequest.FirstName, nameof(registerRequest.FirstName));
       ValidateInput(registerRequest.LastName, nameof(registerRequest.LastName));

       var userExists = await _userManager.FindByEmailAsync(registerRequest.Email);
       if (userExists != null)
       {
           throw new BadRequestException("User with this email already exists");
       }
       
       var newUser = new ApplicationUser
       {
           UserName = registerRequest.Email,
           Email = registerRequest.Email,
       };

       var result = await _userManager.CreateAsync(newUser, registerRequest.Password);
       if (!result.Succeeded)
       {
           throw new BadRequestException("Failed to create user");
       }

       var domainUser = DomainUser.Create(
           registerRequest.Email,
           registerRequest.FirstName,
           registerRequest.LastName,
           newUser.Id
       );

       _dbContext.DomainUsers.Add(domainUser);
       await _dbContext.SaveChangesAsync();

    }
    public async Task<string> LoginAsync(string email, string password)
    {
        var authenticatedUser = await _userManager.FindByEmailAsync(email);
        if (authenticatedUser == null)
        {
            throw new NotFoundException("user", email);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(authenticatedUser, password);
        if (!passwordValid)
        {
            throw new Exception("Invalid password");
        }

        // get user info from the database
        var userFromDb = await _dbContext.DomainUsers.FirstOrDefaultAsync(u => u.AuthId == authenticatedUser.Id);
        if (userFromDb == null)
        {
            throw new NotFoundException("user", email);
        }

        var userInfo = new UserInfo
        {
            UserId = userFromDb.UserId,
            Email = userFromDb.Email,
            FirstName = userFromDb.FirstName,
            LastName = userFromDb.LastName,
            AuthId = userFromDb.AuthId,
            Roles = await _userManager.GetRolesAsync(authenticatedUser) ?? new List<string>()
        };

        var token = _tokenProvider.GenerateToken(userInfo);
        return token;
    }

    public Task LogoutAsync(string authId)
    {
        throw new NotImplementedException();
    }


    public async Task ChangePasswordAsync(string authId, string oldPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(authId.ToString());
        if (user == null)
        {
            throw new NotFoundException("user", authId);
        }

        var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
        if (!result.Succeeded)
        {
            throw new BadRequestException("Failed to change password");
        }
    }

    public async Task DeleteAccountAsync(string authId)
    {
        var user = await _userManager.FindByIdAsync(authId.ToString());
        if (user == null)
        {
            throw new NotFoundException("user", authId);
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new BadRequestException("Failed to delete account");
        }

        // delete the corresponding domain user
        var domainUser = await _dbContext.DomainUsers.FirstOrDefaultAsync(u => u.AuthId == user.Id);
        if (domainUser != null)
        {
            _dbContext.DomainUsers.Remove(domainUser);
            await _dbContext.SaveChangesAsync();
        }
    }

    public Task ResetPasswordAsync(string authId)
    {
        throw new NotImplementedException();
    }

    public Task RefreshTokenAsync(string authId, string refreshToken)
    {
        throw new NotImplementedException();
    }

    private static void ValidateInput(string input, string inputName)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new BadRequestException($"{inputName} cannot be null or empty");
        }
    }
}