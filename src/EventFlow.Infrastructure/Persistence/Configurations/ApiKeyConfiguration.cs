using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Persistence.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.HasKey(k => k.ApiKeyId);

        builder.Property(k => k.HashedKey).IsRequired().HasMaxLength(100);
        builder.Property(k => k.CreatedAt).IsRequired();

        builder.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(50);

        // Configure projectId and Prefix as a unique index to ensure that each project can only have one API key with a given prefix
        builder.HasIndex(k => new { k.ProjectId, k.KeyPrefix }).IsUnique();

    }
}