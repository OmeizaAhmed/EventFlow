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
        }
    }
}