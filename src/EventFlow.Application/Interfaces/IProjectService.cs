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
        Task<bool> ActivateProjectAsync(Guid projectId, Guid userId);
        Task<bool> DeactivateProjectAsync(Guid projectId, Guid userId);
        Task<ProjectResponse> GetProjectByIdAsync(Guid projectId, Guid userId);
        Task<IEnumerable<ProjectResponse>> GetProjectsByUserIdAsync(Guid userId);
        Task<ProjectResponse> UpdateProjectAsync(Guid projectId, Guid userId, string name);
        Task<bool> DeleteProjectAsync(Guid projectId, Guid userId);
        Task<InviteMemberResponse> InviteMemberAsync(Guid inviterId, Guid inviteeId, Guid projectId);
        Task<ProjectMembersResponse> GetProjectMembersAsync(Guid projectId, Guid userId);
        Task<UpdateMemberRoleResponse> UpdateMemberRoleAsync(Guid modifierId, Guid membershipId, Guid projectId, ProjectMembershipRole newRole);

        // Additional methods for API key management
        Task<IEnumerable<ApiKeyResponse>> ListApiKeysAsync(Guid projectId, Guid userId);
        Task<ApiKeyResponse> GetApiKeyByIdAsync(Guid projectId, Guid userId, Guid apiKeyId);
        Task<CreatedApiKeyResponse> GenerateApiKeyAsync(Guid projectId, Guid userId, bool isLive);
        Task<CreatedApiKeyResponse> RotateApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId, bool isLive);
        Task<bool> RevokeApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId);

    }
}
