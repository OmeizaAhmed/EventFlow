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

        public ProjectService(IProjectRepository projectRepository, IApiKeyService apiKeyService, ILogger<ProjectService>? logger = null)
        {
            _projectRepository = projectRepository;
            _apiKeyService = apiKeyService;
            _logger = logger ?? NullLogger<ProjectService>.Instance;
        }

        public async Task<ProjectResponse> CreateProjectAsync(Guid userId, string name)
        {
            var projectCreationResponse = Project.Create(name, userId);
            // Atomically add the project and membership to the repository and return the response
            var project = await _projectRepository.AddAsync(projectCreationResponse);
            _logger.LogInformation("Project {ProjectId} created by user {UserId}", project.ProjectId, userId);
            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt, projectCreationResponse.Membership.Role.ToString());
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

            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt, membership.Role.ToString());
        }

        public async Task<IEnumerable<ProjectResponse>> GetProjectsByUserIdAsync(Guid userId)
        {
            var userProject = await _projectRepository.GetByUserIdAsync(userId);
            
            return userProject.Select(m => new ProjectResponse(m.ProjectId, m.Name, m.CreatedAt, m.Role.ToString()));
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
            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt, membership.Role.ToString());
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

        public async Task<InviteMemberResponse> InviteMemberAsync(Guid inviterId, Guid inviteeId, Guid projectId)
        {
            var inviterMembership = await _projectRepository.GetMembershipAsync(inviterId, projectId);
            if(inviterMembership == null)
            {
                throw new ForbiddenException("Inviter is not a member of the project and cannot invite others.");
            }
            var newMembership = inviterMembership.Invite(inviteeId);
            await _projectRepository.AddMembershipAsync(newMembership);
            _logger.LogInformation("User {InviteeId} invited to project {ProjectId} by user {InviterId}", inviteeId, projectId, inviterId);
            return new InviteMemberResponse(
                newMembership.ProjectMembershipId, 
                newMembership.UserId, 
                newMembership.ProjectId, 
                newMembership.Role.ToString(), 
                newMembership.IsActive, 
                newMembership.CreatedAt, 
                newMembership.UpdatedAt);
        }

        public Task<ProjectMembersResponse> GetProjectMembersAsync(Guid projectId, Guid userId)
        {
            throw new NotImplementedException();
        }

        public async Task<UpdateMemberRoleResponse> UpdateMemberRoleAsync(Guid modifierId, Guid membershipId, Guid projectId, ProjectMembershipRole newRole)
        {
            var membership = await _projectRepository.GetMembershipAsync(membershipId, projectId);
            if (membership == null)
            {
                throw new NotFoundException("Membership", membershipId);
            }

            var modifierMembership = await _projectRepository.GetMembershipAsync(modifierId, projectId);
            if (modifierMembership == null)
            {
                throw new ForbiddenException("Modifier is not a member of the project and cannot update roles.");
            }

            membership.UpdateRole(newRole, modifierMembership);
            await _projectRepository.UpdateMembershipAsync(membership);
            _logger.LogInformation("Membership {MembershipId} role changed to {Role} in project {ProjectId}", membershipId, newRole, projectId);

            return new UpdateMemberRoleResponse(
                membership.ProjectMembershipId,
                membership.UserId,
                membership.ProjectId,
                membership.Role.ToString(),
                membership.IsActive,
                membership.CreatedAt,
                membership.UpdatedAt);
        }
       
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
            var key = await _projectRepository.GetApiKeyAsync(apiKeyId);
            if (key == null)
            {
                throw new NotFoundException("API Key", apiKeyId);
            }
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
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User is not a member of the project and cannot generate API keys.");
            }
            if (!membership.CanManageApiKeys())
            {
                throw new ForbiddenException("User does not have permission to manage API keys.");
            }
            var key = _apiKeyService.Generate(isLive);

            var project = membership.Project;
            
            var apiKey = project.GenerateApiKey(key.HashedKey, key.KeyPrefix, isLive);
            await _projectRepository.AddApiKeyAsync(apiKey);
            _logger.LogInformation("API key {ApiKeyId} created for project {ProjectId} by user {UserId}", apiKey.ApiKeyId, projectId, userId);
            return new CreatedApiKeyResponse(
                apiKey.ApiKeyId,
                apiKey.ProjectId,
                key.PlainTextKey,
                apiKey.KeyPrefix,
                apiKey.IsLive,
                apiKey.CreatedAt
            );
        }

        public async Task<CreatedApiKeyResponse> RotateApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId, bool isLive)
        {
            // Generate a new API key and revoke the old one atomically
            var apiKey = await GenerateApiKeyAsync(projectId, userId, isLive); // This will throw if the user doesn't have permission
            // Revoke the old API key after one hour grace period
            var revokeResult = await RevokeApiKeyAsync(projectId, userId, apiKeyId, DateTime.UtcNow.AddHours(1)); // This will throw if the user doesn't have permission
            _logger.LogInformation("API key {OldApiKeyId} rotated to {NewApiKeyId} for project {ProjectId}", apiKeyId, apiKey.ApiKeyId, projectId);
            return apiKey;
        }

        public async Task<bool> RevokeApiKeyAsync(Guid projectId, Guid userId, Guid apiKeyId, DateTime? revokedAt = null)
        {
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                await EnsureProjectExistsAsync(projectId);
                throw new ForbiddenException("User is not a member of the project and cannot revoke API keys.");
            }
            if (!membership.CanManageApiKeys())
            {
                throw new ForbiddenException("User does not have permission to manage API keys.");
            }
            var apiKey = await _projectRepository.GetApiKeyAsync(apiKeyId);
            if (apiKey == null)
            {
                throw new NotFoundException("API Key", apiKeyId);
            }

            var project = membership.Project;
            var revokedApiKey = project.RevokeApiKey(apiKeyId, revokedAt);
            await _projectRepository.UpdateApiKeyAsync(revokedApiKey);
            _logger.LogInformation("API key {ApiKeyId} revoked for project {ProjectId} by user {UserId}", apiKeyId, projectId, userId);
            return true;
        }

        private async Task EnsureProjectExistsAsync(Guid projectId)
        {
            if (await _projectRepository.GetByIdAsync(projectId) == null)
            {
                throw new NotFoundException("Project", projectId);
            }
        }

    }
}
