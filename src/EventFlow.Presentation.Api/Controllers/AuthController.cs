using Microsoft.AspNetCore.Mvc;
using EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;
using System.Threading.Tasks;

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
        var token = await _authService.LoginUserAsync(loginInput.Email, loginInput.Password);
        return Ok(new { Token = token });
    }
}