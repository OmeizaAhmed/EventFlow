using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.HasKey(k => k.ApiKeyId);

        builder.Property(k => k.HashedKey).IsRequired().HasMaxLength(100);
        builder.Property(k => k.CreatedAt).IsRequired();
    }
}