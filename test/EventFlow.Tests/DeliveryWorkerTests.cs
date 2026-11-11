using System.Collections.Concurrent;
using System.Threading.Channels;
using EventFlow.Application.Interfaces;
using EventFlow.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventFlow.Tests;

public class DeliveryWorkerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task QueuedWork_DrainsBeforePollingWithFreshDisposedScopes()
    {
        await using var fixture = new Fixture();
        fixture.State.Process = (call, _) => Task.FromResult(call <= 3);

        await fixture.Worker.StartAsync(CancellationToken.None);
        var idleTimer = await fixture.Clock.NextTimerAsync();

        Assert.Equal(4, fixture.State.Calls.Count);
        Assert.Equal(4, fixture.State.Calls.Distinct().Count());
        Assert.Equal(fixture.State.Calls.Order(), fixture.State.Disposed.Order());
        Assert.Equal(fixture.Settings.PollInterval, idleTimer.DueTime);
        Assert.Equal(1, fixture.Clock.TimerCount);
        await fixture.StopAsync();
    }

    [Fact]
    public async Task IdlePoll_StopsCleanlyWhenHostCancels()
    {
        await using var fixture = new Fixture();
        fixture.State.Process = (_, _) => Task.FromResult(false);

        await fixture.Worker.StartAsync(CancellationToken.None);
        var idleTimer = await fixture.Clock.NextTimerAsync();
        Assert.Single(fixture.State.Calls);

        await fixture.StopAsync();

        Assert.True(idleTimer.IsDisposed);
        Assert.Single(fixture.State.Calls);
        Assert.Single(fixture.State.Disposed);
        Assert.True(fixture.Worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ProcessorException_DisposesScopeAndResumesAfterPoll()
    {
        await using var fixture = new Fixture();
        fixture.State.Process = (call, _) => call switch
        {
            1 => Task.FromException<bool>(new InvalidOperationException("Temporary database outage")),
            2 => Task.FromResult(true),
            _ => Task.FromResult(false)
        };

        await fixture.Worker.StartAsync(CancellationToken.None);
        var errorTimer = await fixture.Clock.NextTimerAsync();
        Assert.Single(fixture.State.Calls);
        Assert.Single(fixture.State.Disposed);

        errorTimer.Fire();
        await fixture.Clock.NextTimerAsync();

        Assert.Equal(3, fixture.State.Calls.Count);
        Assert.Equal(3, fixture.State.Calls.Distinct().Count());
        Assert.Equal(fixture.State.Calls.Order(), fixture.State.Disposed.Order());
        await fixture.StopAsync();
        Assert.True(fixture.Worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DisabledWorker_DoesNotResolveProcessorOrPoll()
    {
        await using var fixture = new Fixture();
        fixture.Settings.Enabled = false;
        fixture.State.Process = (_, _) => throw new InvalidOperationException("Disabled worker must not process");

        await fixture.Worker.StartAsync(CancellationToken.None);
        await fixture.Worker.ExecuteTask!.WaitAsync(TestTimeout);

        Assert.Empty(fixture.State.Created);
        Assert.Empty(fixture.State.Calls);
        Assert.Equal(0, fixture.Clock.TimerCount);
        Assert.True(fixture.Worker.ExecuteTask.IsCompletedSuccessfully);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private bool _stopped;
        public ProcessorState State { get; } = new();
        public ControlledClock Clock { get; } = new();
        public DeliveryProcessingOptions Settings { get; } = new() { WorkerCount = 1 };
        public DeliveryWorker Worker { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddScoped<IDeliveryProcessor>(_ => new ProbeProcessor(State));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Worker = new DeliveryWorker(_provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(Settings), Clock, NullLogger<DeliveryWorker>.Instance);
        }

        public async Task StopAsync()
        {
            if (_stopped) return;
            using var timeout = new CancellationTokenSource(TestTimeout);
            await Worker.StopAsync(timeout.Token).WaitAsync(TestTimeout);
            if (Worker.ExecuteTask is { } execution)
                await execution.WaitAsync(TestTimeout);
            _stopped = true;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync();
            }
            finally
            {
                Worker.Dispose();
                await _provider.DisposeAsync();
            }
        }
    }

    private sealed class ProcessorState
    {
        public ConcurrentQueue<Guid> Created { get; } = new();
        public ConcurrentQueue<Guid> Calls { get; } = new();
        public ConcurrentQueue<Guid> Disposed { get; } = new();
        public Func<int, CancellationToken, Task<bool>> Process { get; set; } = null!;
    }

    private sealed class ProbeProcessor : IDeliveryProcessor, IAsyncDisposable
    {
        private readonly ProcessorState _state;
        private readonly Guid _instanceId = Guid.NewGuid();

        public ProbeProcessor(ProcessorState state)
        {
            _state = state;
            state.Created.Enqueue(_instanceId);
        }

        public Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            _state.Calls.Enqueue(_instanceId);
            return _state.Process(_state.Calls.Count, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _state.Disposed.Enqueue(_instanceId);
            return ValueTask.CompletedTask;
        }
    }

    // The worker's delay only progresses when the test fires a timer. No sleeps
    // or polling races are needed; every synchronization wait has a time limit.
    private sealed class ControlledClock : TimeProvider
    {
        private readonly Channel<ControlledTimer> _timers = Channel.CreateUnbounded<ControlledTimer>();
        private int _timerCount;
        public int TimerCount => Volatile.Read(ref _timerCount);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ControlledTimer(callback, state, dueTime);
            Interlocked.Increment(ref _timerCount);
            _timers.Writer.TryWrite(timer);
            return timer;
        }

        public Task<ControlledTimer> NextTimerAsync() =>
            _timers.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
    }

    private sealed class ControlledTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        private int _disposed;
        public TimeSpan DueTime { get; } = dueTime;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        public void Fire()
        {
            if (!IsDisposed) callback(state);
        }

        public bool Change(TimeSpan nextDueTime, TimeSpan period) => !IsDisposed;
        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
