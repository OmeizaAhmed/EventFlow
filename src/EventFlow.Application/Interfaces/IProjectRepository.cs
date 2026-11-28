using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventFlow.Domain.Entities;
using EventFlow.Application.DTOs;
using EventFlow.Application.Notifications;

namespace EventFlow.Application.Interfaces
{
    public interface IProjectRepository
    {
        Task<Project?> GetByIdAsync(Guid projectId);
        Task<IEnumerable<ProjectWithUserRole>> GetByUserIdAsync(Guid userId);
        Task<Project> AddAsync(ProjectCreationResponse projectCreationResponse );
        Task<Project> UpdateAsync(Project project);
        Task<bool> DeleteAsync(Guid projectId);
        // Returns an active membership with its Project loaded.
        Task<ProjectMembership?> GetMembershipAsync(Guid userId, Guid projectId);
        Task<ProjectMembership?> GetMembershipByIdAsync(Guid projectId, Guid membershipId);
        Task<IReadOnlyList<ProjectMembership>> ListMembershipsAsync(Guid projectId);
        Task<bool> HasMembershipAsync(Guid projectId, Guid userId);
        Task<bool> UserExistsAsync(Guid userId);
        Task<Guid?> FindUserIdByEmailAsync(string email);
        Task<ProjectInvitation?> GetInvitationAsync(Guid invitationId);
        Task<Guid?> GetInvitedProjectIdAsync(Guid invitationId, Guid inviteeId);
        Task<IReadOnlyList<ProjectInvitation>> ListPendingInvitationsAsync(Guid projectId, DateTime now);
        Task<bool> HasPendingInvitationAsync(Guid projectId, Guid inviteeId, DateTime now);
        Task AddInvitationAsync(ProjectInvitation invitation);
        Task<T> WithProjectLockAsync<T>(Guid projectId, Func<Task<T>> operation);
        Task<IReadOnlyList<DeliveryFailureRecipient>> GetDeliveryFailureRecipientsAsync(Guid projectId,
            CancellationToken cancellationToken);
        Task<ProjectMembership> AddMembershipAsync(ProjectMembership membership);
        Task<ProjectMembership> UpdateMembershipAsync(ProjectMembership membership);
        Task<bool> DeleteMembershipAsync(Guid membershipId);
        Task SaveChangesAsync();

        // Additional methods for API key management
        Task<ApiKey?> GetApiKeyAsync(Guid projectId, Guid apiKeyId);
        Task<ApiKey> AddApiKeyAsync(ApiKey apiKey);
        Task<ApiKey> UpdateApiKeyAsync(ApiKey apiKey);
        Task<bool> DeleteApiKeyAsync(Guid apiKeyId);
        Task<IEnumerable<ApiKey>> GetApiKeysByProjectIdAsync(Guid projectId);
        Task<ApiKey?> GetApiKeyByPrefixAsync(Guid projectId, string keyPrefix);
    }
}
