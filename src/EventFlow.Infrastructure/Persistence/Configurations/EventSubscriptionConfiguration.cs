using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventFlow.Domain.Entities;

public class EventSubscriptionConfiguration : IEntityTypeConfiguration<EventSubscription>
{
    public void Configure(EntityTypeBuilder<EventSubscription> builder)
    {
        builder.ToTable("EventSubscriptions");
        builder.HasKey(s => s.EventSubscriptionId);

        builder.Property(s => s.EventType)
               .HasConversion<string>()   // enum stored as readable text, not an int
               .HasMaxLength(100);

        // A given endpoint can't subscribe to the same event type twice —
        // enforces at the DB level what WebhookEndpoint.Subscribe() already checks in memory
        builder.HasIndex(s => new { s.EndpointId, s.EventType }).IsUnique();
    }
}