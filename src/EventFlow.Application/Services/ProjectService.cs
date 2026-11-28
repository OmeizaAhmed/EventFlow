using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventFlow.Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IApiKeyService _apiKeyService;
        private readonly ILogger<ProjectService> _logger;
        private readonly TimeProvider _time;

        public ProjectService(IProjectRepository projectRepository, IApiKeyService apiKeyService,
            ILogger<ProjectService>? logger = null, TimeProvider? time = null)
        {
            _projectRepository = projectRepository;
            _apiKeyService = apiKeyService;
            _logger = logger ?? NullLogger<ProjectService>.Instance;
            _time = time ?? TimeProvider.System;
        }

        public async Task<ProjectResponse> CreateProjectAsync(Guid userId, string name)
        {
            var projectCreationResponse = Project.Create(name, userId);
            // Atomically add the project and membership to the repository and return the response
            var project = await _projectRepository.AddAsync(projectCreationResponse);
            _logger.LogInformation("Project {ProjectId} created by user {UserId}", project.ProjectId, userId);
            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt, projectCreationResponse.Membership.Role.ToString(), project.IsActive);
        }

        public async Task<ProjectResponse> GetProjectByIdAsync(Guid projectId, Guid userId)
        {
            // Check if the user is a member of the project before returning the project details
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                throw new NotFoundException("Project", $"{projectId}");
            }
            var project = membership.Project;

            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt, membership.Role.ToString(), project.IsActive);
        }

        public async Task<IEnumerable<ProjectResponse>> GetProjectsByUserIdAsync(Guid userId)
        {
            var userProject = await _projectRepository.GetByUserIdAsync(userId);
            
            return userProject.Select(m => new ProjectResponse(m.ProjectId, m.Name, m.CreatedAt, m.Role, m.IsActive));
        }
        public async Task<ProjectResponse> UpdateProjectAsync(Guid projectId,Guid userId, string name)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if(membership == null)
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User is not a member of the project and cannot update it.");
            }
            if (!membership.CanEditProject())
            {
                throw new ForbiddenException("User does not have permission to update the project.");
            }
            var project = membership.Project;
            project.UpdateProject(name);
            await _projectRepository.UpdateAsync(project);
            _logger.LogInformation("Project {ProjectId} updated by user {UserId}", projectId, userId);
            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt, membership.Role.ToString(), project.IsActive);
        }

        public async Task<bool> DeleteProjectAsync(Guid projectId, Guid userId )
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User is not a member of the project and cannot delete it.");
            }

            // check if the user has the necessary role to delete the project
            if (!membership.CanDeleteProject())
            {
                throw new ForbiddenException("User does not have permission to delete the project.");
            }

            await _projectRepository.DeleteAsync(membership.Project.ProjectId);
            _logger.LogInformation("Project {ProjectId} deleted by user {UserId}", projectId, userId);
            return true;
        }

        public Task<ProjectInvitationResponse> InviteMemberAsync(Guid inviterId, Guid inviteeId, Guid projectId,
            ProjectMembershipRole role = ProjectMembershipRole.ReadOnly) =>
            InviteMemberAsync(inviterId, projectId, role, async () =>
            {
                if (inviteeId == Guid.Empty)
                    throw new ValidationException("User is required");
                if (!await _projectRepository.UserExistsAsync(inviteeId))
                    throw new NotFoundException("User", inviteeId);
                return inviteeId;
            });

        public Task<ProjectInvitationResponse> InviteMemberByEmailAsync(Guid inviterId, string email, Guid projectId,
            ProjectMembershipRole role = ProjectMembershipRole.ReadOnly) =>
            InviteMemberAsync(inviterId, projectId, role, async () =>
            {
                var address = email.Trim();
                if (address.Length > 100 || !System.Net.Mail.MailAddress.TryCreate(address, out var parsed) ||
                    !string.Equals(parsed.Address, address, StringComparison.Ordinal))
                    throw new ValidationException("A valid invitee email is required");
                return await _projectRepository.FindUserIdByEmailAsync(address)
                    ?? throw new NotFoundException("User", address);
            });

        private async Task<ProjectInvitationResponse> InviteMemberAsync(Guid inviterId, Guid projectId,
            ProjectMembershipRole role, Func<Task<Guid>> resolveInviteeId)
        {
            var invitation = await _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var inviter = await RequireMembershipManagerAsync(projectId, inviterId);
                var inviteeId = await resolveInviteeId();
                var now = _time.GetUtcNow().UtcDateTime;
                var pending = ProjectInvitation.Create(inviter, inviteeId, role, now);
                if (await _projectRepository.HasMembershipAsync(projectId, inviteeId))
                    throw new ConflictException("This user already has a membership in the project.");
                if (await _projectRepository.HasPendingInvitationAsync(projectId, inviteeId, now))
                    throw new ConflictException("This user already has a pending invitation.");
                await _projectRepository.AddInvitationAsync(pending);
                return ToInvitationResponse(pending, inviter.Project.Name, now);
            });

            _logger.LogInformation("Invitation {InvitationId} created for project {ProjectId} by user {InviterId}",
                invitation.InvitationId, projectId, inviterId);
            return invitation;
        }

        public async Task<ProjectInvitationResponse> GetInvitationAsync(Guid invitationId, Guid userId)
        {
            var invitation = await RequireInvitationAsync(invitationId);
            if (invitation.InviteeUserId != userId)
            {
                var viewer = await _projectRepository.GetMembershipAsync(userId, invitation.ProjectId);
                if (viewer?.CanManageMemberships() != true)
                    throw new NotFoundException("Invitation", invitationId);
            }
            return ToInvitationResponse(invitation, invitation.Project.Name, _time.GetUtcNow().UtcDateTime,
                isInvitee: invitation.InviteeUserId == userId);
        }

        public async Task<IReadOnlyList<ProjectInvitationResponse>> ListPendingInvitationsAsync(Guid projectId,
            Guid userId)
        {
            var manager = await RequireMembershipManagerAsync(projectId, userId);
            var now = _time.GetUtcNow().UtcDateTime;
            var pending = await _projectRepository.ListPendingInvitationsAsync(projectId, now);
            return pending.Select(invitation => ToInvitationResponse(invitation, manager.Project.Name, now)).ToArray();
        }

        public Task RevokeInvitationAsync(Guid projectId, Guid invitationId, Guid userId) =>
            _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var manager = await RequireMembershipManagerAsync(projectId, userId);
                var invitation = await RequireInvitationAsync(invitationId);
                if (invitation.ProjectId != projectId)
                    throw new NotFoundException("Invitation", invitationId);
                if (invitation.Role == ProjectMembershipRole.Admin && manager.Role != ProjectMembershipRole.Owner)
                    throw new ForbiddenException("Only the project owner can revoke an admin invitation.");
                invitation.Revoke(_time.GetUtcNow().UtcDateTime);
                await _projectRepository.SaveChangesAsync();
                return true;
            });

        public async Task<ProjectMemberResponse> AcceptInvitationAsync(Guid invitationId, Guid inviteeId)
        {
            var projectId = await _projectRepository.GetInvitedProjectIdAsync(invitationId, inviteeId)
                ?? throw new NotFoundException("Invitation", invitationId);

            return await _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var invitation = await RequireInvitationAsync(invitationId);
                if (invitation.AcceptedMembershipId is { } acceptedMembership)
                    return ToMemberResponse(await RequireMembershipAsync(projectId, acceptedMembership));

                var now = _time.GetUtcNow().UtcDateTime;
                invitation.EnsurePending(now);
                var inviter = await _projectRepository.GetMembershipAsync(invitation.InviterUserId, projectId);
                if (inviter is null || !inviter.CanManageMemberships() ||
                    (invitation.Role == ProjectMembershipRole.Admin && inviter.Role != ProjectMembershipRole.Owner))
                    throw new ConflictException("The inviter can no longer grant this role. Ask for a new invitation.");
                if (await _projectRepository.HasMembershipAsync(projectId, inviteeId))
                    throw new ConflictException("This account already has a membership in the project.");

                var membership = inviter.Invite(inviteeId, invitation.Role);
                invitation.Accept(membership.ProjectMembershipId, now);
                await _projectRepository.AddMembershipAsync(membership);
                return ToMemberResponse(membership);
            });
        }

        private async Task<ProjectInvitation> RequireInvitationAsync(Guid invitationId) =>
            await _projectRepository.GetInvitationAsync(invitationId)
                ?? throw new NotFoundException("Invitation", invitationId);

        private static ProjectInvitationResponse ToInvitationResponse(ProjectInvitation invitation, string projectName,
            DateTime now, bool isInvitee = false)
        {
            var status = invitation.StatusAt(now);
            return new(invitation.Id, invitation.ProjectId, projectName, invitation.InviteeUserId, invitation.InviterUserId,
                invitation.Role.ToString(), status, invitation.CreatedAt, invitation.ExpiresAt, isInvitee && status == "Pending");
        }

        public async Task<ProjectMembersResponse> GetProjectMembersAsync(Guid projectId, Guid userId)
        {
            await RequireMembershipReaderAsync(projectId, userId);
            var members = await _projectRepository.ListMembershipsAsync(projectId);
            return new ProjectMembersResponse(projectId, members.Select(ToMemberResponse).ToArray());
        }

        public async Task<ProjectMemberResponse> GetProjectMemberAsync(Guid projectId, Guid userId, Guid membershipId)
        {
            await RequireMembershipReaderAsync(projectId, userId);
            return ToMemberResponse(await RequireMembershipAsync(projectId, membershipId));
        }

        public async Task<UpdateMemberRoleResponse> UpdateMemberRoleAsync(Guid modifierId, Guid membershipId,
            Guid projectId, ProjectMembershipRole newRole)
        {
            var membership = await _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var modifier = await RequireMembershipManagerAsync(projectId, modifierId);
                var target = await RequireMembershipAsync(projectId, membershipId);
                target.UpdateRole(newRole, modifier);
                return await _projectRepository.UpdateMembershipAsync(target);
            });

            _logger.LogInformation("Membership {MembershipId} role changed to {Role} in project {ProjectId}", membershipId, newRole, projectId);
            return new UpdateMemberRoleResponse(membership.ProjectMembershipId, membership.UserId, membership.ProjectId,
                membership.Role.ToString(), membership.IsActive, membership.CreatedAt, membership.UpdatedAt);
        }

        public async Task<ProjectMemberResponse> SetMemberStatusAsync(Guid modifierId, Guid membershipId,
            Guid projectId, bool isActive)
        {
            var membership = await _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var modifier = await RequireMembershipManagerAsync(projectId, modifierId);
                var target = await RequireMembershipAsync(projectId, membershipId);
                if (isActive)
                    target.Reactivate(modifier);
                else
                    target.Deactivate(modifier);
                await _projectRepository.SaveChangesAsync();
                return target;
            });

            _logger.LogInformation("Membership {MembershipId} active status set to {IsActive} in project {ProjectId}",
                membershipId, isActive, projectId);
            return ToMemberResponse(membership);
        }

        public async Task<ProjectMemberResponse> TransferOwnershipAsync(Guid ownerId, Guid membershipId, Guid projectId)
        {
            var membership = await _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var owner = await RequireMembershipManagerAsync(projectId, ownerId);
                if (owner.Role != ProjectMembershipRole.Owner)
                    throw new ForbiddenException("Only the active project owner can transfer ownership.");
                var target = await RequireMembershipAsync(projectId, membershipId);
                target.TransferOwnership(owner);
                await _projectRepository.SaveChangesAsync();
                return target;
            });

            _logger.LogInformation("Ownership of project {ProjectId} transferred to membership {MembershipId} by user {UserId}",
                projectId, membershipId, ownerId);
            return ToMemberResponse(membership);
        }

        private async Task RequireMembershipReaderAsync(Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership is null || !membership.CanReadProject())
                throw new NotFoundException("Project", projectId);
        }

        private async Task<ProjectMembership> RequireMembershipManagerAsync(Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership is null || !membership.CanManageMemberships())
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User does not have permission to manage project memberships.");
            }
            return membership;
        }

        private async Task<ProjectMembership> RequireMembershipAsync(Guid projectId, Guid membershipId) =>
            await _projectRepository.GetMembershipByIdAsync(projectId, membershipId)
                ?? throw new NotFoundException("Membership", membershipId);

        private static ProjectMemberResponse ToMemberResponse(ProjectMembership membership) =>
            new(membership.ProjectMembershipId, membership.UserId, membership.ProjectId, membership.Role.ToString(),
                membership.IsActive, membership.CreatedAt, membership.UpdatedAt,
                membership.User?.FirstName, membership.User?.LastName);
       
        public async Task<bool> DeactivateProjectAsync(Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User is not a member of the project and cannot deactivate it.");
            }
            if (!membership.CanToggleProjectStatus())
            {
                throw new ForbiddenException("User does not have permission to deactivate the project.");
            }
            var project = membership.Project;
            project.Deactivate();
            await _projectRepository.UpdateAsync(project);
            _logger.LogInformation("Project {ProjectId} deactivated by user {UserId}", projectId, userId);
            return true;
        }

        public async Task<bool> ActivateProjectAsync(Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User is not a member of the project and cannot activate it.");
            }
            if (!membership.CanToggleProjectStatus())
            {
                throw new ForbiddenException("User does not have permission to activate the project.");
            }
            var project = membership.Project;
            project.Activate();
            await _projectRepository.UpdateAsync(project);
            _logger.LogInformation("Project {ProjectId} activated by user {UserId}", projectId, userId);
            return true;
        }

        // Additional methods for API key management would go here, following similar patterns of validation and repository interaction.
        public async Task<IEnumerable<ApiKeyResponse>> ListApiKeysAsync(Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                throw new ForbiddenException("User is not a member of the project and cannot list API keys.");
            }
            if (!membership.CanManageApiKeys())
            {
                throw new ForbiddenException("User does not have permission to manage API keys.");
            }
            var keys = await _projectRepository.GetApiKeysByProjectIdAsync(projectId);
            return keys.Select(k => new ApiKeyResponse(
                k.ApiKeyId,
                k.ProjectId,
                k.KeyPrefix,
                k.IsLive,
                k.CreatedAt,
                k.RevokedAt,
                k.IsRevoked
            ));
        }

        public async Task<ApiKeyResponse> GetApiKeyByIdAsync(Guid projectId, Guid userId, Guid apiKeyId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                throw new ForbiddenException("User is not a member of the project and cannot list API keys.");
            }
            if (!membership.CanManageApiKeys())
            {
                throw new ForbiddenException("User does not have permission to manage API keys.");
            }
            var key = await RequireProjectKeyAsync(projectId, apiKeyId);
            return new ApiKeyResponse(
                key.ApiKeyId,
                key.ProjectId,
                key.KeyPrefix,
                key.IsLive,
                key.CreatedAt,
                key.RevokedAt,
                key.IsRevoked
            );
        }

        public async Task<CreatedApiKeyResponse> GenerateApiKeyAsync(Guid projectId, Guid userId, bool isLive)
        {
            var project = await RequireApiKeyManagerAsync(projectId, userId);
            var result = await CreateApiKeyAsync(project, isLive);
            _logger.LogInformation("API key {ApiKeyId} generated for project {ProjectId} by user {UserId}",
                result.ApiKeyId, projectId, userId);
            return result;
        }

        public async Task<CreatedApiKeyResponse> RotateApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId, bool isLive)
        {
            var apiKey = await _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var project = await RequireApiKeyManagerAsync(projectId, userId);
                var previous = await RequireProjectKeyAsync(projectId, apiKeyId);
                if (previous.RevokedAt is not null)
                    throw new ConflictException("This API key is already revoked or scheduled for revocation.");

                // Persist the tracked old key and its replacement together.
                project.RevokeApiKey(previous.ApiKeyId, DateTime.UtcNow.AddHours(1));
                return await CreateApiKeyAsync(project, isLive);
            });
            _logger.LogInformation("API key {OldApiKeyId} rotated to {NewApiKeyId} for project {ProjectId}",
                apiKeyId, apiKey.ApiKeyId, projectId);
            return apiKey;
        }

        public async Task<bool> RevokeApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId, DateTime? revokedAt = null)
        {
            await _projectRepository.WithProjectLockAsync(projectId, async () =>
            {
                var project = await RequireApiKeyManagerAsync(projectId, userId);
                var key = await RequireProjectKeyAsync(projectId, apiKeyId);
                var revoked = project.RevokeApiKey(key.ApiKeyId, revokedAt);
                await _projectRepository.UpdateApiKeyAsync(revoked);
                return true;
            });
            _logger.LogInformation("API key {ApiKeyId} revoked for project {ProjectId} by user {UserId}",
                apiKeyId, projectId, userId);
            return true;
        }

        private async Task<CreatedApiKeyResponse> CreateApiKeyAsync(Project project, bool isLive)
        {
            var generated = _apiKeyService.Generate(isLive);
            var key = project.GenerateApiKey(generated.HashedKey, generated.KeyPrefix, isLive);
            await _projectRepository.AddApiKeyAsync(key);
            return new CreatedApiKeyResponse(key.ApiKeyId, key.ProjectId, generated.PlainTextKey,
                key.KeyPrefix, key.IsLive, key.CreatedAt);
        }

        private async Task<Project> RequireApiKeyManagerAsync(Guid projectId, Guid userId)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership is null)
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User is not a member of the project and cannot manage API keys.");
            }
            if (!membership.CanManageApiKeys())
                throw new ForbiddenException("User does not have permission to manage API keys.");

            return membership.Project;
        }

        private async Task<ApiKey> RequireProjectKeyAsync(Guid projectId, Guid apiKeyId) =>
            await _projectRepository.GetApiKeyAsync(projectId, apiKeyId)
                ?? throw new NotFoundException("API Key", apiKeyId);

        private async Task EnsureProjectExistsAsync(Guid projectId)
        {
            if (await _projectRepository.GetByIdAsync(projectId) == null)
            {
                throw new NotFoundException("Project", projectId);
            }
        }

    }
}
