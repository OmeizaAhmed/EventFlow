using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Enums;

namespace EventFlow.Application.Interfaces
{
    public interface IProjectService
    {
        Task<ProjectResponse> CreateProjectAsync(Guid userId, string name);
        Task<ProjectResponse?> GetProjectByIdAsync(Guid projectId, Guid userId);
        Task<ProjectResponse> UpdateProjectAsync(Guid projectId, string name);
        Task<bool> DeleteProjectAsync(Guid projectId, Guid userId);
        Task<InviteMemberResponse> InviteMemberAsync(Guid inviterId, Guid inviteeId, Guid projectId);
        Task<ProjectMembersResponse> GetProjectMembersAsync(Guid projectId, Guid userId);
        Task<UpdateMemberRoleResponse> UpdateMemberRoleAsync(Guid modifierId, Guid membershipId, Guid projectId, ProjectMembershipRole newRole);
    }
}
