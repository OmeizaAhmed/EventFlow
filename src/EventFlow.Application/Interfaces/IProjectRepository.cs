using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventFlow.Domain.Entities;

namespace EventFlow.Application.Interfaces
{
    public interface IProjectRepository
    {
        Task<Project?> GetByIdAsync(Guid projectId);
        Task<Project> AddAsync(ProjectCreationResponse projectCreationResponse );
        Task<Project> UpdateAsync(Project project);
        Task<bool> DeleteAsync(Guid projectId);
        Task<ProjectMembership?> GetMembershipAsync(Guid userId, Guid projectId);
        Task<ProjectMembership> AddMembershipAsync(ProjectMembership membership);
        Task<ProjectMembership> UpdateMembershipAsync(ProjectMembership membership);
        Task<bool> DeleteMembershipAsync(Guid membershipId);
        Task SaveChangesAsync();
    }
}
