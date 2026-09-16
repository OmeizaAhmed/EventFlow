namespace EventFlow.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using EventFlow.Domain.ValueObject;
using EventFlow.Infrastructure.Interfaces;
public class TokenService: ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public string GenerateToken(UserInfo userInfo)
    {
         // 1. Fetch parameters from configuration
        var secretKey = _configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
        var issuer = _configuration["JwtSettings:Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
        var audience = _configuration["JwtSettings:Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");
        var expirationMinutes = double.Parse(_configuration["JwtSettings:ExpirationInMinutes"] ?? "60");

        // sign the token using the secret key and other parameters
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, userInfo.UserId.ToString()),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.GivenName, userInfo.FirstName),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.FamilyName, userInfo.LastName),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Email, userInfo.Email),
            new System.Security.Claims.Claim("AuthId", userInfo.AuthId.ToString()),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in userInfo.Roles)
        {
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            claims: claims,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}