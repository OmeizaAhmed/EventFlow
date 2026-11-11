using EventFlow.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventFlow.Infrastructure.Services;

public sealed class DeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DeliveryProcessingOptions> options,
    TimeProvider time,
    ILogger<DeliveryWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Background delivery processing is disabled");
            return Task.CompletedTask;
        }

        return Task.WhenAll(Enumerable.Range(0, settings.WorkerCount)
            .Select(_ => RunAsync(stoppingToken)));
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Each attempt gets an independent DbContext and tracked claim.
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDeliveryProcessor>();
                if (await processor.ProcessNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Background delivery processing failed; polling will resume");
            }

            try
            {
                await Task.Delay(options.Value.PollInterval, time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
