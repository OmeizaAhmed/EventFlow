using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProjectInvitationConfiguration : IEntityTypeConfiguration<ProjectInvitation>
{
    public void Configure(EntityTypeBuilder<ProjectInvitation> builder)
    {
        builder.ToTable("ProjectInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.HasOne(invitation => invitation.Project).WithMany()
            .HasForeignKey(invitation => invitation.ProjectId);
        builder.HasOne(invitation => invitation.Invitee).WithMany()
            .HasForeignKey(invitation => invitation.InviteeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DomainUser>().WithMany()
            .HasForeignKey(invitation => invitation.InviterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(invitation => new { invitation.ProjectId, invitation.InviteeUserId, invitation.ExpiresAt });
    }
}
