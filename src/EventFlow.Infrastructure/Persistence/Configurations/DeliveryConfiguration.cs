using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;
public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("Deliveries");
        builder.HasKey(d => d.DeliveryId);

        builder.HasMany(d => d.Attempts)
               .WithOne()                          // no inverse nav property needed on DeliveryAttempt
               .HasForeignKey(a => a.DeliveryId)
               .OnDelete(DeleteBehavior.Cascade);   // deleting a Delivery deletes its Attempts

        builder.Metadata
               .FindNavigation(nameof(Delivery.Attempts))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(d => d.AttemptCount);        // derived, not a column
    }
}