using EventFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventFlow.Infrastructure.Persistence.Configurations;

internal sealed class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable("NotificationOutbox");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Name).HasMaxLength(128);
        builder.HasIndex(message => new { message.FailedAt, message.AvailableAt });
    }
}
