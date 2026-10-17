using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<DomainUser>
    {
        public void Configure(EntityTypeBuilder<DomainUser> builder)
        {
            // table name
            builder.ToTable("DomainUsers");
            builder.HasKey(u => u.UserId);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(100);
            builder.Property(u => u.AuthId).IsRequired();
            builder.Property(u => u.CreatedAt).IsRequired();
            builder.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
            builder.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            // Keep the Identity dependency in Infrastructure, out of the domain entity.
            builder.HasOne<EventFlow.Infrastructure.Identity.ApplicationUser>()
                .WithOne(u => u.DomainUser)
                .HasForeignKey<DomainUser>(u => u.AuthId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(u => u.ProjectMemberships).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(u => u.InvitedMemberships).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(u => u.LastModifiedMemberships).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(u => u.RefreshTokens).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
