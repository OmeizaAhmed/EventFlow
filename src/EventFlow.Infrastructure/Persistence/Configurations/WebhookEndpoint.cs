namespace EventFlow.Infrastructure.Persistence.Configurations;

using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("WebhookEndpoints");
        builder.HasKey(e => e.EndpointId);

        builder.Property(e => e.Url).IsRequired().HasMaxLength(2048);

        // SigningSecret is a value object embedded in this table, not its own table
        builder.OwnsOne(e => e.SigningSecret, sb =>
        {
            sb.Property(s => s.Value).HasColumnName("SigningSecret").IsRequired();
        });

        builder.HasMany(e => e.Subscriptions)
               .WithOne()
               .HasForeignKey(s => s.EndpointId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(WebhookEndpoint.Subscriptions))!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}