using Microsoft.AspNetCore.Mvc;
using EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;

namespace EventFlow.Presentation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterInput registerInput)
    {
        await _authService.RegisterUserAsync(registerInput);
        return Ok();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginInput loginInput)
    {
        var auth = await _authService.LoginUserAsync(loginInput.Email, loginInput.Password);
        AddRefreshCookies(auth.RefreshToken, Response);
        return Ok(new { Token = auth.AccessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = GetRefreshTokenFromCookies(Request);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized();
        }
        var auth = await _authService.RefreshTokenAsync(refreshToken);
        AddRefreshCookies(auth.RefreshToken, Response);
        return Ok(new { Token = auth.AccessToken });
    }

    private void AddRefreshCookies(string refreshToken, HttpResponse response)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void RemoveRefreshCookies(HttpResponse response)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(-1)
        };
        response.Cookies.Append("refreshToken", "", cookieOptions);
    }

    private string? GetRefreshTokenFromCookies(HttpRequest request)
    {
        const string RefreshTokenCookieName = "refreshToken";
        if (request?.Cookies != null && request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken))
        {
            return refreshToken;
        }
        return null;
    }


}