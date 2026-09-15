namespace EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;

using EventFlow.Domain.Exceptions;

public class ProjectMembership
{
    public Guid ProjectMembershipId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }
    public ProjectMembershipRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid? InvitedByUserId { get; private set; }
    public Guid? LastModifiedByUserId { get; private set; }

    private ProjectMembership() { }

    public static ProjectMembership Invite(
        Guid userId,
        Guid projectId,
        ProjectMembershipRole role,
        Guid? invitedByUserId = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("User is required");

        if (projectId == Guid.Empty)
            throw new DomainException("Project is required");

        ValidateRole(role);

        return new ProjectMembership
        {
            ProjectMembershipId = Guid.NewGuid(),
            UserId = userId,
            ProjectId = projectId,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            InvitedByUserId = invitedByUserId,
            LastModifiedByUserId = invitedByUserId
        };
    }

    public void UpdateRole(ProjectMembershipRole newRole, Guid modifiedByUserId)
    {
        if (!IsActive)
            throw new DomainException("Cannot update role for an inactive membership");

        ValidateRole(newRole);
        // check if modifiedByUserId is valid and has permission to update the role
        if (modifiedByUserId == Guid.Empty)
            throw new DomainException("Modified by user is required");
        if (Role == ProjectMembershipRole.Owner && newRole != ProjectMembershipRole.Owner)
            throw new DomainException("Owner role cannot be changed through a normal role update. Use transfer ownership");

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = modifiedByUserId;
    }

    public void TransferOwnership(ProjectMembershipRole newRole, Guid changedByUserId)
    {
        if (Role != ProjectMembershipRole.Owner)
            throw new DomainException("Only the current owner can transfer ownership");

        if (newRole != ProjectMembershipRole.Owner && newRole != ProjectMembershipRole.Admin)
            throw new DomainException("Ownership can only be transferred to Owner or Admin roles");

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = changedByUserId;
    }

    public void Deactivate(Guid modifiedByUserId)
    {
        if (!IsActive)
            throw new DomainException("Membership is already inactive");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = modifiedByUserId;
    }

    public void Reactivate(Guid modifiedByUserId)
    {
        if (IsActive)
            throw new DomainException("Membership is already active");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        LastModifiedByUserId = modifiedByUserId;
    }

    public bool CanManageMemberships()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin;

    public bool CanEditProject()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin || Role == ProjectMembershipRole.Developer;

    public bool CanReadProject()
        => Role == ProjectMembershipRole.Owner || Role == ProjectMembershipRole.Admin || Role == ProjectMembershipRole.Developer || Role == ProjectMembershipRole.ReadOnly;

    private static void ValidateRole(ProjectMembershipRole role)
    {
        if (!Enum.IsDefined(typeof(ProjectMembershipRole), role))
            throw new DomainException("Invalid project role");
    }
}

// public enum ProjectMembershipRole
// {
//     Owner = 1,
//     Admin = 2,
//     Developer = 3,
//     ReadOnly = 4
// }
