namespace EventFlow.Infrastructure.Persistence.Configurations;

using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventType)
               .HasConversion<string>()
               .HasMaxLength(100);

        builder.Property(e => e.Payload)
               .HasColumnType("jsonb")     // Postgres-native JSON storage/querying
               .IsRequired();

        builder.Property(e => e.IdempotencyKey).HasMaxLength(200);

        // Idempotency only needs to be unique per project, not globally —
        // two different customers could legitimately reuse the same key value
        builder.HasIndex(e => new { e.ProjectId, e.IdempotencyKey }).IsUnique();
    }
}