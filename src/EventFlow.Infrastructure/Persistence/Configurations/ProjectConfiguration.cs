
using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventFlow.Infrastructure.Persistence.Configurations;
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.ProjectId);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

        builder.HasMany(p => p.ApiKeys)
               .WithOne(k => k.Project)
               .HasForeignKey(k => k.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Endpoints)
               .WithOne(e => e.Project)
               .HasForeignKey(e => e.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Subscriptions)
               .WithOne(s => s.Project)
               .HasForeignKey(s => s.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Project.ApiKeys))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Project.Endpoints))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Project.Subscriptions))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Events)
               .WithOne(e => e.Project)
               .HasForeignKey(e => e.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
