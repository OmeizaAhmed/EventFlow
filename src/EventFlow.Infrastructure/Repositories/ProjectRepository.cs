using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EventFlow.Application.DTOs;
using EventFlow.Application.Notifications;
using EventFlow.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using EventFlow.Infrastructure.Services;

namespace EventFlow.Infrastructure.Repositories
{
    public class ProjectRepository: IProjectRepository
    {
        private readonly EventFlowDbContext _context;
        private readonly ILookupNormalizer _lookupNormalizer;
        private readonly NotificationOutbox _notifications;

        public ProjectRepository(EventFlowDbContext context, ILookupNormalizer lookupNormalizer,
            NotificationOutbox notifications)
        {
            _context = context;
            _lookupNormalizer = lookupNormalizer;
            _notifications = notifications;
        }

        public async Task<IEnumerable<ProjectWithUserRole>> GetByUserIdAsync(Guid userId)
        {
            var projectWithUserRoles = await _context.ProjectMemberships
                .Include(m => m.Project)
                .Where(m => m.UserId == userId && m.IsActive)
                .Select(m => new ProjectWithUserRole(
                    m.ProjectId,
                    m.Project.Name,
                    m.Project.CreatedAt,
                    m.Role.ToString(),
                    m.Project.IsActive
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
            return await _context.ProjectMemberships.Include(m => m.Project)
                .FirstOrDefaultAsync(m => m.UserId == userId && m.ProjectId == projectId && m.IsActive);
        }

        public Task<ProjectMembership?> GetMembershipByIdAsync(Guid projectId, Guid membershipId) =>
            _context.ProjectMemberships.Include(m => m.User).SingleOrDefaultAsync(m =>
                m.ProjectId == projectId && m.ProjectMembershipId == membershipId);

        public async Task<IReadOnlyList<ProjectMembership>> ListMembershipsAsync(Guid projectId) =>
            await _context.ProjectMemberships.AsNoTracking().Include(m => m.User).Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.Role).ThenBy(m => m.CreatedAt).ThenBy(m => m.ProjectMembershipId)
                .ToListAsync();

        public Task<bool> UserExistsAsync(Guid userId) =>
            _context.DomainUsers.AnyAsync(user => user.UserId == userId);

        public Task<Guid?> FindUserIdByEmailAsync(string email)
        {
            var normalizedEmail = _lookupNormalizer.NormalizeEmail(email);
            return _context.ApplicationUsers.Where(user => user.NormalizedEmail == normalizedEmail && user.DomainUser != null)
                .Select(user => (Guid?)user.DomainUser!.UserId)
                .FirstOrDefaultAsync();
        }

        public Task<bool> HasMembershipAsync(Guid projectId, Guid userId) =>
            _context.ProjectMemberships.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        public Task<ProjectInvitation?> GetInvitationAsync(Guid invitationId) =>
            _context.Set<ProjectInvitation>().Include(invitation => invitation.Project)
                .Include(invitation => invitation.Invitee)
                .SingleOrDefaultAsync(invitation => invitation.Id == invitationId);

        public Task<Guid?> GetInvitedProjectIdAsync(Guid invitationId, Guid inviteeId) =>
            _context.Set<ProjectInvitation>().Where(invitation =>
                invitation.Id == invitationId && invitation.InviteeUserId == inviteeId)
                .Select(invitation => (Guid?)invitation.ProjectId).SingleOrDefaultAsync();

        public async Task<IReadOnlyList<ProjectInvitation>> ListPendingInvitationsAsync(Guid projectId, DateTime now) =>
            await PendingInvitations(projectId, now).AsNoTracking()
                .OrderByDescending(invitation => invitation.CreatedAt).ThenBy(invitation => invitation.Id)
                .ToListAsync();

        public Task<bool> HasPendingInvitationAsync(Guid projectId, Guid inviteeId, DateTime now) =>
            PendingInvitations(projectId, now).AnyAsync(invitation => invitation.InviteeUserId == inviteeId);

        public async Task AddInvitationAsync(ProjectInvitation invitation)
        {
            _context.Add(invitation);
            _notifications.Stage(new ProjectInvitationCreated(invitation.Id));
            await _context.SaveChangesAsync();
        }

        private IQueryable<ProjectInvitation> PendingInvitations(Guid projectId, DateTime now) =>
            _context.Set<ProjectInvitation>().Where(invitation => invitation.ProjectId == projectId &&
                invitation.AcceptedAt == null && invitation.RevokedAt == null && invitation.ExpiresAt > now);

        public async Task<IReadOnlyList<DeliveryFailureRecipient>> GetDeliveryFailureRecipientsAsync(Guid projectId,
            CancellationToken cancellationToken) =>
            await _context.ProjectMemberships.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.IsActive && m.Role == ProjectMembershipRole.Owner)
                .Select(m => new DeliveryFailureRecipient(m.User.Email, m.Project.Name))
                .Distinct().ToListAsync(cancellationToken);

        public async Task<T> WithProjectLockAsync<T>(Guid projectId, Func<Task<T>> operation)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // Serialize project-scoped operations that authorize and change multiple records.
            await _context.Projects.Where(project => project.ProjectId == projectId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.UpdatedAt, project => project.UpdatedAt));
            var result = await operation();
            await transaction.CommitAsync();
            return result;
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
        public Task<ApiKey?> GetApiKeyAsync(Guid projectId, Guid apiKeyId) =>
            _context.ApiKeys.SingleOrDefaultAsync(key => key.ProjectId == projectId && key.ApiKeyId == apiKeyId);
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
        public async Task<ApiKey?> GetApiKeyByPrefixAsync(Guid projectId,string keyPrefix)
        {
            return await _context.ApiKeys.FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyPrefix == keyPrefix);
        }

       
    }
}
