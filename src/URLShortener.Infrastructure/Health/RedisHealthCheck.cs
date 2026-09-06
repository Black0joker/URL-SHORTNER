using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using URLShortener.Infrastructure.Redis;

namespace URLShortener.Infrastructure.Health;

/// <summary>
/// Readiness probe for Redis. When Redis is not configured the check reports Degraded
/// (the API still serves traffic by falling back to SQL Server).
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly RedisOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public RedisHealthCheck(IOptions<RedisOptions> options, IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return HealthCheckResult.Degraded("Redis is not configured; redirects fall back to SQL Server.");

        try
        {
            var redis = (IConnectionMultiplexer)_serviceProvider.GetService(typeof(IConnectionMultiplexer))!;
            var db = redis.GetDatabase();
            var latency = await db.PingAsync();

            return HealthCheckResult.Healthy($"Redis reachable. Ping: {latency.TotalMilliseconds:F1} ms.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis check failed.", ex);
        }
    }
}
