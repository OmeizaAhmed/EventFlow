namespace EventFlow.Application.Interfaces;

public interface INotification
{
    static abstract string Name { get; }
}

public interface INotificationPublisher
{
    /// <summary>Persists a notification for asynchronous delivery to its subscribers.</summary>
    Task PublishAsync<T>(T notification, CancellationToken cancellationToken = default) where T : INotification;
}

public interface INotificationHandler<in T> where T : INotification
{
    Task HandleAsync(T notification, CancellationToken cancellationToken);
}
