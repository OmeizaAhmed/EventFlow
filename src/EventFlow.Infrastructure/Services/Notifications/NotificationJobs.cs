using EventFlow.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventFlow.Infrastructure.Services;

public sealed class NotificationJobs(
    EventFlowDbContext context,
    IServiceProvider services,
    IBackgroundJobClient jobs,
    TimeProvider time,
    ILogger<NotificationJobs> logger)
{
    internal const string Queue = "notifications";
    internal const string SweepJobId = "eventflow-notification-outbox";
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromMinutes(1);

    [Queue(Queue)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var dueIds = await Due(time.GetUtcNow().UtcDateTime)
            .OrderBy(message => message.AvailableAt).ThenBy(message => message.Id)
            .Select(message => message.Id)
            .Take(100).ToListAsync(cancellationToken);

        foreach (var id in dueIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            jobs.Enqueue<NotificationJobs>(job => job.DispatchAsync(id, CancellationToken.None));
        }
    }

    [Queue(Queue)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task DispatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var leaseToken = Guid.NewGuid();
        var claimed = await Due(now).Where(message => message.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.LeaseToken, (Guid?)leaseToken)
                .SetProperty(message => message.AvailableAt, now + LeaseDuration), cancellationToken);
        if (claimed == 0)
            return;

        var owned = context.Set<NotificationMessage>().Where(message => message.Id == id && message.LeaseToken == leaseToken);
        var message = await owned.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (message is null)
            return;

        using var timeout = new CancellationTokenSource(DispatchTimeout, time);
        using var dispatchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var dispatcher = services.GetRequiredKeyedService<INotificationDispatch>(message.Name);
            await dispatcher.DispatchAsync(message.Payload, dispatchCancellation.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(owned, message, exception, cancellationToken);
            return;
        }

        await owned.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(IQueryable<NotificationMessage> owned,
        NotificationMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var attempt = message.AttemptCount + 1;
        var now = time.GetUtcNow().UtcDateTime;
        var retryAt = now.AddMinutes(Math.Pow(2, attempt - 1));
        var exhausted = attempt >= MaxAttempts;
        var saved = await owned.ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.AttemptCount, attempt)
            .SetProperty(row => row.LeaseToken, (Guid?)null)
            .SetProperty(row => row.AvailableAt, retryAt)
            .SetProperty(row => row.FailedAt, exhausted ? now : (DateTime?)null), cancellationToken);
        if (saved == 0)
            return;

        logger.Log(exhausted ? LogLevel.Error : LogLevel.Warning,
            "Notification {NotificationId} ({NotificationName}) failed on attempt {Attempt}: {ErrorType}. Exhausted: {Exhausted}",
            message.Id, message.Name, attempt, exception.GetType().Name, exhausted);
    }

    private IQueryable<NotificationMessage> Due(DateTime now) =>
        context.Set<NotificationMessage>().Where(message => message.FailedAt == null && message.AvailableAt <= now);
}
