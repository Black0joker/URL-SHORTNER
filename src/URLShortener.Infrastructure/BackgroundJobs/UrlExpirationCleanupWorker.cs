using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using URLShortener.Application.Interfaces;

namespace URLShortener.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically deactivates expired URLs (time-based lifecycle management).
/// Runs on a fixed interval; failures are logged and retried on the next cycle
/// so a single bad cycle never crashes the worker.
/// </summary>
public class UrlExpirationCleanupWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UrlExpirationCleanupWorker> _logger;

    public UrlExpirationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<UrlExpirationCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("URL expiration cleanup worker started. Interval: {Interval}", Interval);

        using var timer = new PeriodicTimer(Interval);

        // Run once at startup, then on each tick.
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IExpiredUrlCleanupService>();

                var deactivated = await cleanup.DeactivateExpiredUrlsAsync(stoppingToken);

                if (deactivated > 0)
                    _logger.LogInformation("Deactivated {Count} expired URL(s).", deactivated);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log and continue; the next tick retries. A transient failure must not kill the worker.
                _logger.LogError(ex, "URL expiration cleanup cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
