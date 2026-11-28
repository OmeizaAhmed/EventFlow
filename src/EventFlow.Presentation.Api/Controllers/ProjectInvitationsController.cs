using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Presentation.Api.Controllers;

[ApiController]
[Route("api/project-invitations")]
[Authorize(Policy = "ValidUserId")]
public sealed class ProjectInvitationsController(IProjectService projects, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("{invitationId:guid}")]
    public async Task<IActionResult> Get(Guid invitationId) =>
        Ok(await projects.GetInvitationAsync(invitationId, currentUser.UserId));

    [HttpPost("{invitationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid invitationId) =>
        Ok(await projects.AcceptInvitationAsync(invitationId, currentUser.UserId));

    [HttpGet("/api/projects/{projectId:guid}/invitations")]
    public async Task<IActionResult> List(Guid projectId) =>
        Ok(await projects.ListPendingInvitationsAsync(projectId, currentUser.UserId));

    [HttpDelete("/api/projects/{projectId:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> Revoke(Guid projectId, Guid invitationId)
    {
        await projects.RevokeInvitationAsync(projectId, invitationId, currentUser.UserId);
        return NoContent();
    }
}
