using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventFlow.Domain.Entities;
using EventFlow.Application.DTOs;

namespace EventFlow.Application.Interfaces
{
    public interface IProjectRepository
    {
        Task<Project?> GetByIdAsync(Guid projectId);
        Task<IEnumerable<ProjectWithUserRole>> GetByUserIdAsync(Guid userId);
        Task<Project> AddAsync(ProjectCreationResponse projectCreationResponse );
        Task<Project> UpdateAsync(Project project);
        Task<bool> DeleteAsync(Guid projectId);
        Task<ProjectMembership?> GetMembershipAsync(Guid userId, Guid projectId);
        Task<ProjectMembership> AddMembershipAsync(ProjectMembership membership);
        Task<ProjectMembership> UpdateMembershipAsync(ProjectMembership membership);
        Task<bool> DeleteMembershipAsync(Guid membershipId);
        Task SaveChangesAsync();

        // Additional methods for API key management
        Task<ApiKey?> GetApiKeyAsync(Guid apiKeyId);
        Task<ApiKey> AddApiKeyAsync(ApiKey apiKey);
        Task<ApiKey> UpdateApiKeyAsync(ApiKey apiKey);
        Task<bool> DeleteApiKeyAsync(Guid apiKeyId);
        Task<IEnumerable<ApiKey>> GetApiKeysByProjectIdAsync(Guid projectId);
        Task<ApiKey?> GetApiKeyByPrefixAsync(Guid projectId, string keyPrefix);
    }
}
