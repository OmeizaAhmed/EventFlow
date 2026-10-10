using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Persistence.Configurations
{
    public class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
    {
        public void Configure(EntityTypeBuilder<ProjectMembership> builder)
        {
            builder.ToTable("ProjectMemberships");
            builder.HasKey(pm => pm.ProjectMembershipId);
            builder.Property(pm => pm.ProjectId).IsRequired();
            builder.Property(pm => pm.UserId).IsRequired();
            builder.Property(pm => pm.Role).IsRequired();
            builder.Property(pm => pm.InvitedByUserId).IsRequired(false);
            builder.Property(pm => pm.LastModifiedByUserId).IsRequired(false);
            builder.Property(pm => pm.CreatedAt).IsRequired();
            builder.Property(pm => pm.UpdatedAt).IsRequired();
            builder.HasIndex(pm => new { pm.ProjectId, pm.UserId }).IsUnique();

            // relationships
            builder.HasOne(pm => pm.Project)
            .WithMany(p => p.ProjectMemberships)
            .HasForeignKey(pm => pm.ProjectId)
            .IsRequired();

            builder.HasOne(pm => pm.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(pm => pm.UserId)
                .IsRequired();


        }
    }
}