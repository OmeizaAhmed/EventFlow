using EventFlow.Application.Interfaces;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Presentation.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/members")]
[Authorize(Policy = "ValidUserId")]
public sealed class ProjectMembersController(IProjectService projects, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId) =>
        Ok(await projects.GetProjectMembersAsync(projectId, currentUser.UserId));

    [HttpGet("{membershipId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, Guid membershipId) =>
        Ok(await projects.GetProjectMemberAsync(projectId, currentUser.UserId, membershipId));

    [HttpPost]
    public async Task<IActionResult> Invite(Guid projectId, InviteProjectMember request)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(request.Email);
        if (hasEmail == request.InviteeId.HasValue)
            throw new BadRequestException("Supply either an inviteeId or an email.");

        var role = request.Role is null ? ProjectMembershipRole.ReadOnly : ParseRole(request.Role);
        var invitation = request.InviteeId is { } inviteeId
            ? await projects.InviteMemberAsync(currentUser.UserId, inviteeId, projectId, role)
            : await projects.InviteMemberByEmailAsync(currentUser.UserId, request.Email!, projectId, role);
        return CreatedAtAction(nameof(ProjectInvitationsController.Get), "ProjectInvitations",
            new { invitationId = invitation.InvitationId }, invitation);
    }

    [HttpPut("{membershipId:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid projectId, Guid membershipId, ChangeProjectMemberRole request) =>
        Ok(await projects.UpdateMemberRoleAsync(currentUser.UserId, membershipId, projectId, ParseRole(request.Role)));

    [HttpPost("{membershipId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid projectId, Guid membershipId) =>
        Ok(await projects.SetMemberStatusAsync(currentUser.UserId, membershipId, projectId, isActive: false));

    [HttpPost("{membershipId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid projectId, Guid membershipId) =>
        Ok(await projects.SetMemberStatusAsync(currentUser.UserId, membershipId, projectId, isActive: true));

    [HttpPost("{membershipId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid projectId, Guid membershipId) =>
        Ok(await projects.TransferOwnershipAsync(currentUser.UserId, membershipId, projectId));

    private static ProjectMembershipRole ParseRole(string role)
    {
        if (!Enum.TryParse<ProjectMembershipRole>(role, ignoreCase: true, out var parsed) ||
            !string.Equals(Enum.GetName(parsed), role, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Role must be Owner, Admin, Developer, or ReadOnly.");
        return parsed;
    }

    public sealed record InviteProjectMember(Guid? InviteeId, string? Email, string? Role);
    public sealed record ChangeProjectMemberRole(string Role);
}
