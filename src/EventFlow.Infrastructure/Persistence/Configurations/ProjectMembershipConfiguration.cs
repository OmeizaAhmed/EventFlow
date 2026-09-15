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

            // relationships
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(pm => pm.ProjectId);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(pm => pm.UserId);
        }
    }
}