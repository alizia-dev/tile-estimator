using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TileEstimator.Application.Abstractions;

namespace TileEstimator.Infrastructure.Jobs;

/// <summary>
/// A simple in-process job queue (SPEC 2: an abstraction with a simple local implementation).
/// Work is queued without blocking the request; swapping in Hangfire or Azure Functions later
/// means replacing this class, not the call sites.
/// </summary>
public sealed class BackgroundJobScheduler(ILogger<BackgroundJobScheduler> logger) : IBackgroundJobScheduler
{
    private readonly ConcurrentQueue<QueuedJob> _queue = new();

    internal record QueuedJob(Func<IServiceProviderAccessor, CancellationToken, Task> Work, string Description);

    public void Enqueue(Func<IServiceProviderAccessor, CancellationToken, Task> work, string description)
    {
        ArgumentNullException.ThrowIfNull(work);
        _queue.Enqueue(new QueuedJob(work, description));
        logger.LogDebug("Background job queued: {Description}", description);
    }

    internal bool TryDequeue(out QueuedJob? job) => _queue.TryDequeue(out job);
}

/// <summary>Drains the job queue. A failing job is logged and dropped, never retried forever.</summary>
public sealed class BackgroundJobRunner(
    IBackgroundJobScheduler scheduler,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundJobRunner> logger)
    : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (scheduler is not BackgroundJobScheduler queue)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!queue.TryDequeue(out var job) || job is null)
            {
                await Task.Delay(IdleDelay, stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                await job.Work(new ScopedServiceProviderAccessor(scope.ServiceProvider), stoppingToken);
                logger.LogDebug("Background job finished: {Description}", job.Description);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // A failing job must not take the host down with it.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Background job failed: {Description}", job.Description);
            }
        }
    }

    private sealed class ScopedServiceProviderAccessor(IServiceProvider provider) : IServiceProviderAccessor
    {
        public T GetRequiredService<T>() where T : notnull => provider.GetRequiredService<T>();
    }
}
