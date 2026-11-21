using System.Security.Claims;
using EventFlow.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.IntegrationTests;

// Test-only route exercises the real authentication handler for both key environments.
[ApiController]
[Route("api/projects/{projectId}/events/apikey-test")]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme, Policy = ApiKeyDefaults.AnyKeyPolicy)]
public sealed class ApiKeyProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        projectId = User.FindFirstValue(ApiKeyDefaults.ProjectIdClaim),
        apiKeyId = User.FindFirstValue(ApiKeyDefaults.ApiKeyIdClaim),
        environment = User.FindFirstValue(ApiKeyDefaults.EnvironmentClaim)
    });
}
