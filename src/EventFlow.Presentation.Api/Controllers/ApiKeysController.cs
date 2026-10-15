using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace EventFlow.Presentation.Api.Controllers
{
    [Route("api/projects/{projectId}/[controller]")]
    [ApiController]
    [Authorize(Policy = "ValidUserId")]
    public class ApiKeysController : ControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly ICurrentUser _currentUser;
        public ApiKeysController(IProjectService projectService, ICurrentUser currentUser)
        {
            _projectService = projectService;
            _currentUser = currentUser;
        }
        // create api key for a project
        [HttpPost]
        public async Task<IActionResult> CreateApiKey(Guid projectId, [FromBody] CreateApiKeyRequest request)
        {
            var response = await _projectService.GenerateApiKeyAsync(projectId, _currentUser.UserId, request.IsLive);
            return CreatedAtAction(nameof(GetApiKeyById), new { projectId = projectId, apiKeyId = response.ApiKeyId }, response);
        }

        // get api key by id

        [HttpGet("{apiKeyId}")]
        public async Task<IActionResult> GetApiKeyById(Guid projectId, Guid apiKeyId)
        {
            var response = await _projectService.GetApiKeyByIdAsync(projectId, _currentUser.UserId, apiKeyId);
            return Ok(response);
        }

        // get all api keys for a project
        [HttpGet]
        public async Task<IActionResult> GetApiKeys(Guid projectId)
        {
            var response = await _projectService.ListApiKeysAsync(projectId, _currentUser.UserId);
            return Ok(response);
        }

        // revoke api key
        [HttpPost("{apiKeyId}/revoke")]
        public async Task<IActionResult> RevokeApiKey(Guid projectId, Guid apiKeyId)
        {
            await _projectService.RevokeApiKeyAsync(projectId, _currentUser.UserId, apiKeyId);
            return NoContent();
        }
        // rotate api key
        [HttpPost("{apiKeyId}/rotate")]
        public async Task<IActionResult> RotateApiKey(Guid projectId, Guid apiKeyId, [FromBody] CreateApiKeyRequest request)
        {
            var response = await _projectService.RotateApiKeyAsync(projectId, _currentUser.UserId, apiKeyId, request.IsLive);
            return Ok(response);
        }
    }
    public record CreateApiKeyRequest(bool IsLive);
}
