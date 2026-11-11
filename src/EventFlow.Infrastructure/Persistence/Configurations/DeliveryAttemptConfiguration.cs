using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Persistence.Configurations;

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("DeliveryAttempts");
        builder.HasKey(a => a.DeliveryAttemptId);
        builder.Property(a => a.DeliveryAttemptId).ValueGeneratedNever();
        builder.HasIndex(a => new { a.DeliveryId, a.AttemptNumber }).IsUnique();

        builder.Property(a => a.AttemptNumber).IsRequired();
        builder.Property(a => a.AttemptedAt).IsRequired();
    }
}
