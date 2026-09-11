using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("DeliveryAttempts");
        builder.HasKey(a => a.DeliveryAttemptId);

        builder.Property(a => a.AttemptNumber).IsRequired();
        builder.Property(a => a.AttemptedAt).IsRequired();
    }
}