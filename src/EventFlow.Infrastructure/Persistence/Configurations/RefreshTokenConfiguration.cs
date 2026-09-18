using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Persistence.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(rt => rt.Id);
            builder.Property(rt => rt.UserId).IsRequired();
            builder.Property(rt => rt.Token).IsRequired();
            builder.Property(rt => rt.ExpiresAt).IsRequired();
            builder.Property(rt => rt.RevokedAt);
            builder.Property(rt => rt.CreatedAt).IsRequired();
            builder.Property(rt => rt.LastUsedAt).IsRequired();
        }
    }
}