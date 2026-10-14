using EventFlow.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EventFlow.Presentation.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/[controller]s")]
public class EventController: ControllerBase
{

    [Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme, Policy = ApiKeyDefaults.AnyKeyPolicy)]
    [HttpGet("apikey-test")]
    public async Task<IActionResult> TestApiKey(Guid projectId)
    {

        return Ok(new { projectId = User.FindFirstValue(ApiKeyDefaults.ProjectIdClaim), apiKeyId = User.FindFirstValue(ApiKeyDefaults.ApiKeyIdClaim), environment = User.FindFirstValue(ApiKeyDefaults.EnvironmentClaim) });
    }

}

