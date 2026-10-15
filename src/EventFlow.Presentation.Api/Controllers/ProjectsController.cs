using Microsoft.AspNetCore.Mvc;
using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using EventFlow.Application.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EventFlow.Infrastructure.Authentication;
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ValidUserId")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ICurrentUser _currentUser;

    public ProjectsController(IProjectService projectService, ICurrentUser currentUser)
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

    

    

    public record CreateProjectRequest(string Name);
  
}
