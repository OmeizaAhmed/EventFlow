using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.DTOs
{
    public record ProjectRequest(string Name);
    public record ProjectResponse(Guid ProjectId, string Name, DateTime CreatedAt);
    public record ProjectMembershipRequest(Guid UserId, Guid ProjectId);
    public record ProjectMembersResponse(Guid ProjectId, IEnumerable<Guid> MemberIds);
    public record InviteMemberRequest(Guid InviterId, Guid InviteeId, Guid ProjectId);
    public record InviteMemberResponse(Guid ProjectMembershipId, Guid UserId, Guid ProjectId, string Role, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
    public record UpdateMemberRoleRequest(Guid ModifierId, Guid ProjectMembershipId, string NewRole);
    public record UpdateMemberRoleResponse(Guid ProjectMembershipId, Guid UserId, Guid ProjectId, string Role, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
}
