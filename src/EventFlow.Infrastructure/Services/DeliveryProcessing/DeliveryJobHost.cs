using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EventFlow.Infrastructure.Services;

internal sealed class DeliveryJobHost(IServiceProvider services, IOptions<DeliveryProcessingOptions> options,
    IOptions<NotificationOptions> notifications)
    : IHostedService, IDisposable
{
    private BackgroundJobServerHostedService? _server;
    private BackgroundJobServerHostedService? _notificationServer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.Enabled)
        {
            services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<DeliveryJobs>(
                DeliveryJobs.RecoveryJobId, DeliveryJobs.Queue,
                job => job.RecoverAsync(CancellationToken.None), Cron.Minutely());
            var interval = settings.PollInterval < TimeSpan.FromMinutes(1)
                ? settings.PollInterval
                : TimeSpan.FromMinutes(1);
            _server = CreateServer(DeliveryJobs.Queue, settings.WorkerCount, interval);
            await _server.StartAsync(cancellationToken);
        }

        if (notifications.Value.Enabled)
        {
            services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<NotificationJobs>(
                NotificationJobs.SweepJobId, NotificationJobs.Queue,
                job => job.SweepAsync(CancellationToken.None), Cron.Minutely());
            _notificationServer = CreateServer(NotificationJobs.Queue, 1, TimeSpan.FromSeconds(15));
            await _notificationServer.StartAsync(cancellationToken);
        }
    }

    private BackgroundJobServerHostedService CreateServer(string queue, int workerCount, TimeSpan interval)
    {
        var serverOptions = new BackgroundJobServerOptions
        {
            Activator = services.GetRequiredService<JobActivator>(),
            Queues = [queue],
            WorkerCount = workerCount,
            SchedulePollingInterval = interval
        };
        return new BackgroundJobServerHostedService(services.GetRequiredService<JobStorage>(), serverOptions,
            [], services.GetRequiredService<IHostApplicationLifetime>());
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_server?.StopAsync(cancellationToken) ?? Task.CompletedTask,
            _notificationServer?.StopAsync(cancellationToken) ?? Task.CompletedTask);

    public void Dispose()
    {
        _server?.Dispose();
        _notificationServer?.Dispose();
    }
}
