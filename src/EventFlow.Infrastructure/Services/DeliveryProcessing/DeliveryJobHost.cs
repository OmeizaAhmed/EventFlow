using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EventFlow.Infrastructure.Services;

internal sealed class DeliveryJobHost(IServiceProvider services, IOptions<DeliveryProcessingOptions> options)
    : IHostedService, IDisposable
{
    private BackgroundJobServerHostedService? _server;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return Task.CompletedTask;

        services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<DeliveryJobs>(
            DeliveryJobs.RecoveryJobId, DeliveryJobs.Queue,
            job => job.RecoverAsync(CancellationToken.None), Cron.Minutely());

        var serverOptions = new BackgroundJobServerOptions
        {
            Activator = services.GetRequiredService<JobActivator>(),
            Queues = [DeliveryJobs.Queue],
            WorkerCount = settings.WorkerCount,
            SchedulePollingInterval = settings.PollInterval < TimeSpan.FromMinutes(1)
                ? settings.PollInterval
                : TimeSpan.FromMinutes(1)
        };
        _server = new BackgroundJobServerHostedService(services.GetRequiredService<JobStorage>(), serverOptions,
            [], services.GetRequiredService<IHostApplicationLifetime>());
        return _server.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _server?.StopAsync(cancellationToken) ?? Task.CompletedTask;

    public void Dispose() => _server?.Dispose();
}
