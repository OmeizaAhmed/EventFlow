using System.Text.Json;
using EventFlow.Application.Interfaces;
using EventFlow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlow.Infrastructure.Services;

public sealed class NotificationOutbox(
    EventFlowDbContext context,
    IServiceScopeFactory scopes,
    TimeProvider time) : INotificationPublisher
{
    public async Task PublishAsync<T>(T notification, CancellationToken cancellationToken = default)
        where T : INotification
    {
        // Standalone publication commits only its own write.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventFlowDbContext>();
        db.Add(CreateMessage(notification));
        await db.SaveChangesAsync(cancellationToken);
    }

    // The caller owns the transaction when publication accompanies another state change.
    internal void Stage<T>(T notification) where T : INotification =>
        context.Add(CreateMessage(notification));

    private NotificationMessage CreateMessage<T>(T notification) where T : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        return new NotificationMessage
        {
            Id = Guid.NewGuid(),
            Name = T.Name,
            Payload = JsonSerializer.Serialize(notification),
            AvailableAt = time.GetUtcNow().UtcDateTime
        };
    }
}
