using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

namespace EventFlow.Infrastructure.Persistence.Configurations;
public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("Deliveries");
        builder.HasKey(d => d.DeliveryId);

        builder.HasMany(d => d.Attempts)
               .WithOne(a => a.Delivery)
               .HasForeignKey(a => a.DeliveryId)
               .OnDelete(DeleteBehavior.Cascade);   // deleting a Delivery deletes its Attempts

        builder.Metadata
               .FindNavigation(nameof(Delivery.Attempts))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(d => d.AttemptCount);        // derived, not a column

        builder.HasOne(d => d.Event)
               .WithMany(e => e.Deliveries)
               .HasForeignKey(d => d.EventId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Endpoint)
               .WithMany(e => e.Deliveries)
               .HasForeignKey(d => d.EndpointId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
