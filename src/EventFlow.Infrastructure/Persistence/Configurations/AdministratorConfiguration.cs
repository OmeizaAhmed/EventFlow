using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Persistence.Configurations
{
    public class AdministratorConfiguration : IEntityTypeConfiguration<Administrator>
    {
        public void Configure(EntityTypeBuilder<Administrator> builder)
        {
            // table name
            builder.ToTable("Administrators");
            builder.HasKey(u => u.AdministratorId);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(100);
            builder.Property(u => u.AuthId).IsRequired();
            builder.Property(u => u.CreatedAt).IsRequired();

            // relationships
            // A developer can have many projects
            builder.HasMany<Project>()
                   .WithOne()
                   .HasForeignKey(p => p.AdministratorId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}