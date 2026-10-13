using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EventFlow.Application.DTOs;

namespace EventFlow.Infrastructure.Repositories
{
    public class ProjectRepository: IProjectRepository
    {
        private readonly EventFlowDbContext _context;

        public ProjectRepository(EventFlowDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProjectWithUserRole>> GetByUserIdAsync(Guid userId)
        {
            var projectWithUserRoles = await _context.ProjectMemberships
                .Include(m => m.Project)
                .Where(m => m.UserId == userId)
                .Select(m => new ProjectWithUserRole(
                    m.ProjectId,
                    m.Project.Name,
                    m.Project.CreatedAt,
                    m.Role.ToString()
                ))
                .ToListAsync();

            return projectWithUserRoles;        
        }
        public async Task<Project?> GetByIdAsync(Guid projectId)
        {
            return await _context.Projects.FindAsync(projectId);

        }
        public async Task<Project> AddAsync(ProjectCreationResponse projectCreationResponse)
        {
            _context.Projects.Add(projectCreationResponse.Project);
            _context.ProjectMemberships.Add(projectCreationResponse.Membership);
            await _context.SaveChangesAsync();
            return projectCreationResponse.Project;
        }
        public async Task<Project> UpdateAsync(Project project)
        {
            _context.Projects.Update(project);
            await _context.SaveChangesAsync();
            return project;
        }   
        public async Task<bool> DeleteAsync(Guid projectId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return false;
            }
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<ProjectMembership?> GetMembershipAsync(Guid userId, Guid projectId)
        {
            return await _context.ProjectMemberships.FirstOrDefaultAsync(m => m.UserId == userId && m.ProjectId == projectId);
        }
        public async Task<ProjectMembership> AddMembershipAsync(ProjectMembership membership)
        {
            _context.ProjectMemberships.Add(membership);
            await _context.SaveChangesAsync();
            return membership;
        }
        public async Task<ProjectMembership> UpdateMembershipAsync(ProjectMembership membership)
        {
            _context.ProjectMemberships.Update(membership);
            await _context.SaveChangesAsync();
            return membership;
        }
        public async Task<bool> DeleteMembershipAsync(Guid membershipId)
        {
            var membership = await _context.ProjectMemberships.FindAsync(membershipId);
            if (membership == null)
            {
                return false;
            }
            _context.ProjectMemberships.Remove(membership);
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
        // Additional methods for API key management
        public async Task<ApiKey?> GetApiKeyAsync(Guid apiKeyId)
        {
            return await _context.ApiKeys.FindAsync(apiKeyId);
        }
        public async Task<ApiKey> AddApiKeyAsync(ApiKey apiKey)
        {
            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();
            return apiKey;
        }
        public async Task<ApiKey> UpdateApiKeyAsync(ApiKey apiKey)
        {
            _context.ApiKeys.Update(apiKey);
            await _context.SaveChangesAsync();
            return apiKey;
        }
        public async Task<bool> DeleteApiKeyAsync(Guid apiKeyId)
        {
            var apiKey = await _context.ApiKeys.FindAsync(apiKeyId);
            if (apiKey == null)
            {
                return false;
            }
            _context.ApiKeys.Remove(apiKey);
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<IEnumerable<ApiKey>> GetApiKeysByProjectIdAsync(Guid projectId)
        {
            return await _context.ApiKeys.Where(k => k.ProjectId == projectId).ToListAsync();
        }
        public async Task<ApiKey?> GetApiKeyByPrefixAsync(Guid projectId, string keyPrefix)
        {
            return await _context.ApiKeys.FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyPrefix == keyPrefix);
        }
    }
}
