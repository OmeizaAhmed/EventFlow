using Hangfire;
using Microsoft.Extensions.Logging;

namespace EventFlow.Infrastructure.Services;

public sealed class DeliveryJobScheduler(
    Func<IBackgroundJobClient?> getClient,
    ILogger<DeliveryJobScheduler> logger)
{
    internal void Enqueue(Guid deliveryId) =>
        getClient()?.Enqueue<DeliveryJobs>(job => job.SendAsync(deliveryId, CancellationToken.None));

    internal void ScheduleRetry(Guid deliveryId, DateTime dueAtUtc)
    {
        // Hangfire stores the schedule score at whole-second precision.
        var fractionalTicks = dueAtUtc.Ticks % TimeSpan.TicksPerSecond;
        if (fractionalTicks != 0)
            dueAtUtc = dueAtUtc.AddTicks(TimeSpan.TicksPerSecond - fractionalTicks);

        getClient()?.Schedule<DeliveryJobs>(job => job.SendAsync(deliveryId, CancellationToken.None),
            new DateTimeOffset(dueAtUtc, TimeSpan.Zero));
    }

    internal void EnqueueCommitted(Guid deliveryId)
    {
        try
        {
            Enqueue(deliveryId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Could not enqueue committed delivery {DeliveryId}; the recovery job will pick it up",
                deliveryId);
        }
    }
}
