namespace EventFlow.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EventFlow.Application.DTOs;
using EventFlow.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

public class TokenServiceTest
{
    private const string ValidSecretKey = "super_secret_key_that_is_at_least_32_bytes_long!";
    private const string ValidIssuer = "EventFlowAuthServer";
    private const string ValidAudience = "EventFlowApiClient";
    private const string ValidExpirationInMinutes = "60";

    private static IConfiguration CreateConfiguration(
        string? secretKey = ValidSecretKey,
        string? issuer = ValidIssuer,
        string? audience = ValidAudience,
        string? expirationInMinutes = ValidExpirationInMinutes)
    {
        var settings = new Dictionary<string, string?>();

        if (secretKey != null) settings["JwtSettings:SecretKey"] = secretKey;
        if (issuer != null) settings["JwtSettings:Issuer"] = issuer;
        if (audience != null) settings["JwtSettings:Audience"] = audience;
        if (expirationInMinutes != null) settings["JwtSettings:ExpirationInMinutes"] = expirationInMinutes;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static UserInfo CreateSampleUserInfo()
    {
        return new UserInfo
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000123"),
            Email = "john.doe@example.com",
            FirstName = "John",
            LastName = "Doe",
            Roles = new List<string> { "Admin", "User" }
        };
    }

    [Fact]
    public void GenerateToken_WithValidUserInfo_ReturnsValidJwtToken()
    {
        // Arrange
        var config = CreateConfiguration();
        var tokenService = new TokenService(config);
        var userInfo = CreateSampleUserInfo();

        // Act
        var tokenString = tokenService.GenerateToken(userInfo);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(tokenString));

        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(tokenString));

        var jwtToken = handler.ReadJwtToken(tokenString);

        Assert.Equal(ValidIssuer, jwtToken.Issuer);
        Assert.Contains(ValidAudience, jwtToken.Audiences);

        var claims = jwtToken.Claims.ToList();

        Assert.Equal("00000000-0000-0000-0000-000000000123", claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal("John", claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value);
        Assert.Equal("Doe", claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value);
        Assert.Equal("john.doe@example.com", claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value);

        var roleClaims = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Equal(2, roleClaims.Count);
        Assert.Contains("Admin", roleClaims);
        Assert.Contains("User", roleClaims);
    }

    [Fact]
    public void GenerateToken_MissingSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(secretKey: null);
        var tokenService = new TokenService(config);
        var userInfo = CreateSampleUserInfo();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => tokenService.GenerateToken(userInfo));
        Assert.Equal("JwtSettings:SecretKey is not configured.", exception.Message);
    }

    [Fact]
    public void GenerateToken_MissingIssuer_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(issuer: null);
        var tokenService = new TokenService(config);
        var userInfo = CreateSampleUserInfo();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => tokenService.GenerateToken(userInfo));
        Assert.Equal("JwtSettings:Issuer is not configured.", exception.Message);
    }

    [Fact]
    public void GenerateToken_MissingAudience_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(audience: null);
        var tokenService = new TokenService(config);
        var userInfo = CreateSampleUserInfo();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => tokenService.GenerateToken(userInfo));
        Assert.Equal("JwtSettings:Audience is not configured.", exception.Message);
    }

    [Fact]
    public void GenerateToken_MissingExpirationInMinutes_UsesDefault60Minutes()
    {
        // Arrange
        var config = CreateConfiguration(expirationInMinutes: null);
        var tokenService = new TokenService(config);
        var userInfo = CreateSampleUserInfo();
        var beforeUtc = DateTime.UtcNow;

        // Act
        var tokenString = tokenService.GenerateToken(userInfo);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        var expectedExpiration = beforeUtc.AddMinutes(60);
        var actualExpiration = jwtToken.ValidTo;

        Assert.True(actualExpiration >= expectedExpiration.AddSeconds(-5) && actualExpiration <= expectedExpiration.AddSeconds(5));
    }

    [Fact]
    public void GenerateToken_CustomExpirationInMinutes_SetsExpirationCorrectly()
    {
        // Arrange
        const string customExpiration = "120";
        var config = CreateConfiguration(expirationInMinutes: customExpiration);
        var tokenService = new TokenService(config);
        var userInfo = CreateSampleUserInfo();
        var beforeUtc = DateTime.UtcNow;

        // Act
        var tokenString = tokenService.GenerateToken(userInfo);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        var expectedExpiration = beforeUtc.AddMinutes(120);
        var actualExpiration = jwtToken.ValidTo;

        Assert.True(actualExpiration >= expectedExpiration.AddSeconds(-5) && actualExpiration <= expectedExpiration.AddSeconds(5));
    }

    
}
