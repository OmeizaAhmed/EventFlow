using System.Text.Json;
using EventFlow.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventFlow.Infrastructure.Services;

public static class NotificationRegistration
{
    public static IServiceCollection AddNotificationHandler<T, THandler>(this IServiceCollection services)
        where T : INotification
        where THandler : class, INotificationHandler<T>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<INotificationHandler<T>, THandler>());
        services.TryAddKeyedScoped<INotificationDispatch, NotificationDispatch<T>>(T.Name);
        return services;
    }
}

internal interface INotificationDispatch
{
    Task DispatchAsync(string payload, CancellationToken cancellationToken);
}

internal sealed class NotificationDispatch<T>(IEnumerable<INotificationHandler<T>> handlers)
    : INotificationDispatch where T : INotification
{
    public async Task DispatchAsync(string payload, CancellationToken cancellationToken)
    {
        var notification = JsonSerializer.Deserialize<T>(payload)
            ?? throw new InvalidOperationException($"Missing payload for notification {T.Name}.");
        foreach (var handler in handlers)
            await handler.HandleAsync(notification, cancellationToken);
    }
}
