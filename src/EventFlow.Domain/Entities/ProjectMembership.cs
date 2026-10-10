namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;

using EventFlow.Domain.Exceptions;
using System.Runtime.CompilerServices;

public class ProjectMembership
{
    public Guid ProjectMembershipId { get; private set; }
    public Guid UserId { get; private set; }
    public DomainUser User { get; private set; } = null!;
    public Guid ProjectId { get; private set; }
    public Project Project { get; private set; } = null!;
    public ProjectMembershipRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid? InvitedByUserId { get; private set; }
    public Guid? LastModifiedByUserId { get; private set; }

    private ProjectMembership() { }
    internal static ProjectMembership Create(Guid userId, Guid projectId)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("User is required");
        if (projectId == Guid.Empty)
            throw new ValidationException("Project is required");
        
        return new ProjectMembership
        {
            ProjectMembershipId = Guid.NewGuid(),
            UserId = userId,
            ProjectId = projectId,
            Role = ProjectMembershipRole.Owner,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            InvitedByUserId = userId,
            LastModifiedByUserId = userId
        };
    }

    
    public ProjectMembership Invite(
        Guid userId, ProjectMembershipRole? role = null)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("User is required");
        if (!this.IsActive)
            throw new ValidationException("Host project membership is inactive");
        if(!this.CanManageMemberships())
            throw new ValidationException("Host project membership does not have permission to invite new members");


        ValidateRole(role ?? ProjectMembershipRole.ReadOnly);

        return new ProjectMembership
        {
            ProjectMembershipId = Guid.NewGuid(),
            UserId = userId,
            ProjectId = this.ProjectId,
            Role = role ?? ProjectMembershipRole.ReadOnly,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            InvitedByUserId = this.UserId,
            LastModifiedByUserId = this.UserId
        };
    }

    public void UpdateRole(ProjectMembershipRole newRole, ProjectMembership Modifier)
    {
        if (!IsActive)
            throw new ValidationException("Cannot update role for an inactive membership");

        ValidateRole(newRole);
        // check if Modifier is valid and has permission to update the role
        if (Modifier == null)
            throw new ValidationException("Modifier is required");
        if (!Modifier.CanManageMemberships())
            throw new ValidationException("Modifier does not have permission to update the role");
        if (Role == ProjectMembershipRole.Owner && newRole != ProjectMembershipRole.Owner)
            throw new ValidationException("Owner role cannot be changed through a normal role update. Use transfer ownership");

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = Modifier.UserId;
    }

    public void TransferOwnership(ProjectMembership Owner)
    {
        if (Owner.Role != ProjectMembershipRole.Owner)
            throw new ValidationException("Only the current owner can transfer ownership");

        if (Role != ProjectMembershipRole.Owner && Role != ProjectMembershipRole.Admin)
            throw new ValidationException("Ownership can only be transferred to Owner or Admin roles");

        Role = ProjectMembershipRole.Owner;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = Owner.UserId;
    }

    public void Deactivate(ProjectMembership Modifier)
    {
        if (!IsActive)
            throw new ValidationException("Membership is already inactive");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = Modifier.UserId;
    }

    public void Reactivate(ProjectMembership Modifier)
    {
        if (IsActive)
            throw new ValidationException("Membership is already active");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = Modifier.UserId;
    }

    public bool CanManageApiKeys()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin;
    public bool CanManageWebhooks()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin;
    
    public bool CanManageEvents()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin || Role == ProjectMembershipRole.Developer;

    public bool CanManageEndpoints()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin;

    public bool CanManageMemberships()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin;
    public bool CanDeleteProject()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin;
    public bool CanEditProject()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin || Role == ProjectMembershipRole.Developer;

    public bool CanReadProject()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin || Role == ProjectMembershipRole.Developer || Role == ProjectMembershipRole.ReadOnly;

    private static void ValidateRole(ProjectMembershipRole role)
    {
        if (!Enum.IsDefined(typeof(ProjectMembershipRole), role))
            throw new ValidationException("Invalid project role");
    }
}

// public enum ProjectMembershipRole
// {
//     Owner = 1,
//     Admin = 2,
//     Developer = 3,
//     ReadOnly = 4
// }
