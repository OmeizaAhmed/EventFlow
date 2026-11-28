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
        Task<ProjectInvitationResponse> InviteMemberAsync(Guid inviterId, Guid inviteeId, Guid projectId,
            ProjectMembershipRole role = ProjectMembershipRole.ReadOnly);
        Task<ProjectInvitationResponse> InviteMemberByEmailAsync(Guid inviterId, string email, Guid projectId,
            ProjectMembershipRole role = ProjectMembershipRole.ReadOnly);
        Task<ProjectInvitationResponse> GetInvitationAsync(Guid invitationId, Guid userId);
        Task<IReadOnlyList<ProjectInvitationResponse>> ListPendingInvitationsAsync(Guid projectId, Guid userId);
        Task RevokeInvitationAsync(Guid projectId, Guid invitationId, Guid userId);
        Task<ProjectMemberResponse> AcceptInvitationAsync(Guid invitationId, Guid inviteeId);
        Task<ProjectMembersResponse> GetProjectMembersAsync(Guid projectId, Guid userId);
        Task<ProjectMemberResponse> GetProjectMemberAsync(Guid projectId, Guid userId, Guid membershipId);
        Task<UpdateMemberRoleResponse> UpdateMemberRoleAsync(Guid modifierId, Guid membershipId, Guid projectId, ProjectMembershipRole newRole);
        Task<ProjectMemberResponse> SetMemberStatusAsync(Guid modifierId, Guid membershipId, Guid projectId, bool isActive);
        Task<ProjectMemberResponse> TransferOwnershipAsync(Guid ownerId, Guid membershipId, Guid projectId);

        // Additional methods for API key management
        Task<IEnumerable<ApiKeyResponse>> ListApiKeysAsync(Guid projectId, Guid userId);
        Task<ApiKeyResponse> GetApiKeyByIdAsync(Guid projectId, Guid userId, Guid apiKeyId);
        Task<CreatedApiKeyResponse> GenerateApiKeyAsync(Guid projectId, Guid userId, bool isLive);
        Task<CreatedApiKeyResponse> RotateApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId, bool isLive);
        Task<bool> RevokeApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId, DateTime? revokedAt = null);

    }
}
