using EventFlow.Infrastructure.Repositories;
using Hangfire;

namespace EventFlow.Infrastructure.Services;

public sealed class DeliveryJobs(
    DeliveryProcessor processor,
    DeliveryRepository repository,
    DeliveryJobScheduler scheduler,
    TimeProvider time)
{
    internal const string Queue = "webhook-deliveries";
    internal const string RecoveryJobId = "eventflow-delivery-recovery";
    private const int RecoveryBatchSize = 100;

    [Queue(Queue)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        var nextRetryAt = await processor.ProcessAsync(deliveryId, cancellationToken);
        if (nextRetryAt is { } due)
            scheduler.ScheduleRetry(deliveryId, due);
    }

    [Queue(Queue)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var due = await repository.ListDueIdsAsync(
            time.GetUtcNow().UtcDateTime, RecoveryBatchSize, cancellationToken);

        foreach (var deliveryId in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scheduler.Enqueue(deliveryId);
        }
    }
}
