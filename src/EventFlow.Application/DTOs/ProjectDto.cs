using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.DTOs
{
    public record ProjectRequest(string Name);
    public record ProjectResponse(Guid ProjectId, string Name, DateTime CreatedAt, string? Role, bool IsActive);
    public record ProjectMembershipRequest(Guid UserId, Guid ProjectId);
    public record ProjectMemberResponse(Guid ProjectMembershipId, Guid UserId, Guid ProjectId, string Role,
        bool IsActive, DateTime CreatedAt, DateTime UpdatedAt, string? FirstName, string? LastName);
    public record ProjectMembersResponse(Guid ProjectId, IReadOnlyList<ProjectMemberResponse> Members);
    public record InviteMemberRequest(Guid InviterId, Guid InviteeId, Guid ProjectId);
    public record ProjectInvitationResponse(Guid InvitationId, Guid ProjectId, string ProjectName, Guid InviteeUserId,
        Guid InviterUserId, string Role, string Status, DateTime CreatedAt, DateTime ExpiresAt, bool CanAccept);
    public record UpdateMemberRoleRequest(Guid ModifierId, Guid ProjectMembershipId, string NewRole);
    public record UpdateMemberRoleResponse(Guid ProjectMembershipId, Guid UserId, Guid ProjectId, string Role, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
    public record ProjectWithUserRole(Guid ProjectId, string Name, DateTime CreatedAt, string Role, bool IsActive);
}
