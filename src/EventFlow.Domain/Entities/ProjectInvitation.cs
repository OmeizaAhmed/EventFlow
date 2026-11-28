using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;

namespace EventFlow.Domain.Entities;

public sealed class ProjectInvitation
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Project Project { get; private set; } = null!;
    public Guid InviteeUserId { get; private set; }
    public DomainUser Invitee { get; private set; } = null!;
    public Guid InviterUserId { get; private set; }
    public ProjectMembershipRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? AcceptedMembershipId { get; private set; }

    private ProjectInvitation() { }

    public static ProjectInvitation Create(ProjectMembership inviter, Guid inviteeUserId,
        ProjectMembershipRole role, DateTime now)
    {
        inviter.EnsureCanInvite(role);
        return new ProjectInvitation
        {
            Id = Guid.NewGuid(),
            ProjectId = inviter.ProjectId,
            InviterUserId = inviter.UserId,
            InviteeUserId = inviteeUserId,
            Role = role,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };
    }

    public string StatusAt(DateTime now) =>
        AcceptedAt is not null ? "Accepted" :
        RevokedAt is not null ? "Revoked" :
        ExpiresAt <= now ? "Expired" : "Pending";

    public void EnsurePending(DateTime now)
    {
        var status = StatusAt(now);
        if (status != "Pending")
            throw new ConflictException($"This invitation is {status.ToLowerInvariant()}.");
    }

    public void Accept(Guid membershipId, DateTime now)
    {
        EnsurePending(now);
        AcceptedAt = now;
        AcceptedMembershipId = membershipId;
    }

    public void Revoke(DateTime now)
    {
        if (RevokedAt is not null)
            return;
        EnsurePending(now);
        RevokedAt = now;
    }
}
