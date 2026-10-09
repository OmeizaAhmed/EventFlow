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

namespace EventFlow.Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;

        public ProjectService(IProjectRepository projectRepository)
        {
            _projectRepository = projectRepository;
        }

        public async Task<ProjectResponse> CreateProjectAsync(Guid userId, string name)
        {
            var projectCreationResponse = Project.Create(name, userId);
            // Atomically add the project and membership to the repository and return the response
            var project = await _projectRepository.AddAsync(projectCreationResponse);
            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt);
        }

        public async Task<ProjectResponse?> GetProjectByIdAsync(Guid projectId, Guid userId)
        {
            // Check if the user is a member of the project before returning the project details
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
                throw new ForbiddenException("User is not a member of the project and cannot view it.");
            }
            // Retrieve the project details
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                throw new NotFoundException("Project", projectId);
            }
            return new ProjectResponse(project.ProjectId, project.Name, project.CreatedAt);
        }

        public async Task<ProjectResponse> UpdateProjectAsync(Guid projectId, string name)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> DeleteProjectAsync(Guid projectId, Guid userId )
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                throw new NotFoundException("Project", projectId);
            }
            // Check if the user is a member of the project before allowing deletion
            var membership = await _projectRepository.GetMembershipAsync(userId, projectId);
            if (membership == null)
            {
               throw new ForbiddenException("User is not a member of the project and cannot delete it.");
            }

            // check if the user has the necessary role to delete the project
            if (!membership.CanDeleteProject())
            {
                throw new ForbiddenException("User does not have permission to delete the project.");
            }

            await _projectRepository.DeleteAsync(project.ProjectId);
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

            return new UpdateMemberRoleResponse(
                membership.ProjectMembershipId,
                membership.UserId,
                membership.ProjectId,
                membership.Role.ToString(),
                membership.IsActive,
                membership.CreatedAt,
                membership.UpdatedAt);
        }
       
    }
}
