namespace EventFlow.Infrastructure.Persistence.Configurations;

using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.ProjectId);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

        builder.HasMany(p => p.ApiKeys)
               .WithOne()
               .HasForeignKey(k => k.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Endpoints)
               .WithOne()
               .HasForeignKey(e => e.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Project.ApiKeys))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Project.Endpoints))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}