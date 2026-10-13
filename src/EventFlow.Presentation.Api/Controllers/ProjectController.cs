using Microsoft.AspNetCore.Mvc;
using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using EventFlow.Application.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
[ApiController]
[Route("api/[controller]s")]
[Authorize(Policy = "ValidUserId")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ICurrentUser _currentUser;

    public ProjectController(IProjectService projectService, ICurrentUser currentUser)
    {
        _projectService = projectService;
        _currentUser = currentUser;
    }

    //create project
    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var response = await _projectService.CreateProjectAsync(_currentUser.UserId, request.Name);
        return CreatedAtAction(nameof(GetProjectById), new { projectId = response.ProjectId }, response);
    }

    // Get all projects for the authenticated user
    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var projects = await _projectService.GetProjectsByUserIdAsync(_currentUser.UserId);
        return Ok(projects);
    }

    [HttpGet("{projectId}")]
    public async Task<IActionResult> GetProjectById(Guid projectId)
    {
        var project = await _projectService.GetProjectByIdAsync(projectId, _currentUser.UserId);
    
        return Ok(project);
    }

    [HttpPut("{projectId}")]
    public async Task<IActionResult> UpdateProject(Guid projectId, [FromBody] ProjectRequest request)
    {
        var project = await _projectService.UpdateProjectAsync(projectId, _currentUser.UserId, request.Name);
        return Ok(project);
    }

    // Activate a project
    [HttpPost("{projectId}/activate")]
    public async Task<IActionResult> ActivateProject(Guid projectId)
    {
        await _projectService.ActivateProjectAsync(projectId, _currentUser.UserId);
        return NoContent();
    }

    // Deactivate a project
    [HttpPost("{projectId}/deactivate")]
    public async Task<IActionResult> DeactivateProject(Guid projectId)
    {
        await _projectService.DeactivateProjectAsync(projectId, _currentUser.UserId);
        return NoContent();
    }

    // create api key for a project
    [HttpPost("{projectId}/apikeys")]
    public async Task<IActionResult> CreateApiKey(Guid projectId, [FromBody] CreateApiKeyRequest request)
    {
        var response = await _projectService.GenerateApiKeyAsync(projectId, _currentUser.UserId, request.IsLive);
        return CreatedAtAction(nameof(GetApiKeyById), new { projectId = projectId, apiKeyId = response.ApiKeyId }, response);
    }

    // get api key by id
    [HttpGet("{projectId}/apikeys/{apiKeyId}")]
    public async Task<IActionResult> GetApiKeyById(Guid projectId, Guid apiKeyId)
    {
        var response = await _projectService.GetApiKeyByIdAsync(projectId, _currentUser.UserId, apiKeyId);
        return Ok(response);
    }

    // get all api keys for a project
    [HttpGet("{projectId}/apikeys")]
    public async Task<IActionResult> GetApiKeys(Guid projectId)
    {
        var response = await _projectService.ListApiKeysAsync(projectId, _currentUser.UserId);
        return Ok(response);
    }

    // revoke api key
    [HttpPost("{projectId}/apikeys/{apiKeyId}/revoke")]
    public async Task<IActionResult> RevokeApiKey(Guid projectId, Guid apiKeyId)
    {
        await _projectService.RevokeApiKeyAsync(projectId, _currentUser.UserId, apiKeyId);
        return NoContent();
    }
    // rotate api key
    [HttpPost("{projectId}/apikeys/{apiKeyId}/rotate")]
    public async Task<IActionResult> RotateApiKey(Guid projectId, Guid apiKeyId, [FromBody] CreateApiKeyRequest request)
    {
        var response = await _projectService.RotateApiKeyAsync(projectId, _currentUser.UserId, apiKeyId, request.IsLive);
        return Ok(response);
    }

    public record CreateProjectRequest(string Name);
    public record CreateApiKeyRequest(bool IsLive);
}
