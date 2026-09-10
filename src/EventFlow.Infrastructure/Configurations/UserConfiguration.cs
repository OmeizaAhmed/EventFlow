using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.UserId);
            builder.Property(u => u.ProjectId).IsRequired();
            builder.Property(u => u.Email).IsRequired().HasMaxLength(100);
            builder.Property(u => u.CreatedAt).IsRequired();
        }
    }
}