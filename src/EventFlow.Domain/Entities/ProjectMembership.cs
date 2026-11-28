namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;

using EventFlow.Domain.Exceptions;

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
    public DomainUser? InvitedByUser { get; private set; }
    public Guid? LastModifiedByUserId { get; private set; }
    public DomainUser? LastModifiedByUser { get; private set; }

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
        EnsureCanInvite(role ?? ProjectMembershipRole.ReadOnly);

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

    internal void EnsureCanInvite(ProjectMembershipRole role)
    {
        if (!CanManageMemberships())
            throw new ValidationException("Host project membership does not have permission to invite new members");
        ValidateRole(role);
        if (role == ProjectMembershipRole.Owner)
            throw new ValidationException("Use ownership transfer to assign the owner role");
        if (role == ProjectMembershipRole.Admin && Role != ProjectMembershipRole.Owner)
            throw new ForbiddenException("Only the project owner can appoint admins");
    }

    public void UpdateRole(ProjectMembershipRole newRole, ProjectMembership modifier)
    {
        if (!IsActive)
            throw new ValidationException("Cannot update role for an inactive membership");

        ValidateRole(newRole);
        EnsureCanModify(modifier);
        if (Role == ProjectMembershipRole.Owner || newRole == ProjectMembershipRole.Owner)
            throw new ValidationException("Use ownership transfer to change the project owner");
        if (newRole == ProjectMembershipRole.Admin && modifier.Role != ProjectMembershipRole.Owner)
            throw new ForbiddenException("Only the project owner can appoint admins");

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = modifier.UserId;
    }

    public void TransferOwnership(ProjectMembership owner)
    {
        if (!owner.IsActive || owner.Role != ProjectMembershipRole.Owner || owner.ProjectId != ProjectId)
            throw new ForbiddenException("Only the active project owner can transfer ownership");

        if (!IsActive || Role != ProjectMembershipRole.Admin)
            throw new ValidationException("Ownership can only be transferred to an active project admin");

        var now = DateTime.UtcNow;
        owner.Role = ProjectMembershipRole.Admin;
        owner.UpdatedAt = now;
        owner.LastModifiedByUserId = owner.UserId;
        Role = ProjectMembershipRole.Owner;
        UpdatedAt = now;
        LastModifiedByUserId = owner.UserId;
    }

    public void Deactivate(ProjectMembership modifier)
    {
        EnsureCanModify(modifier);
        if (Role == ProjectMembershipRole.Owner)
            throw new ValidationException("Transfer ownership before deactivating the project owner");
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = modifier.UserId;
    }

    public void Reactivate(ProjectMembership modifier)
    {
        EnsureCanModify(modifier);
        if (IsActive)
            return;

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = modifier.UserId;
    }

    private void EnsureCanModify(ProjectMembership modifier)
    {
        if (!modifier.CanManageMemberships() || modifier.ProjectId != ProjectId)
            throw new ForbiddenException("User does not have permission to manage this membership");
        if (Role is (ProjectMembershipRole.Owner or ProjectMembershipRole.Admin) &&
            modifier.Role != ProjectMembershipRole.Owner)
            throw new ForbiddenException("Only the project owner can manage owner and admin memberships");
    }

    public bool CanManageApiKeys()
        => CanManageMemberships();
    public bool CanManageWebhooks()
        => CanManageMemberships();
    
    public bool CanManageEvents()
        => IsActive && Role is ProjectMembershipRole.Owner or ProjectMembershipRole.Admin or ProjectMembershipRole.Developer;

    public bool CanManageEndpoints()
        => CanManageMemberships();

    public bool CanManageMemberships()
        => IsActive && Role is ProjectMembershipRole.Owner or ProjectMembershipRole.Admin;
    public bool CanDeleteProject()
        => CanManageMemberships();
    public bool CanEditProject()
        => CanManageEvents();

    public bool CanReadProject()
        => IsActive && Enum.IsDefined(Role);
    public bool CanToggleProjectStatus()
        => CanManageMemberships();
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
