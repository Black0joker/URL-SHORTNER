using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using URLShortener.Application.Common;
using URLShortener.Application.Interfaces;
using URLShortener.Application.Services;
using URLShortener.Infrastructure.Authentication;
using URLShortener.Infrastructure.BackgroundJobs;
using URLShortener.Infrastructure.Health;
using URLShortener.Infrastructure.Persistence;
using URLShortener.Infrastructure.RateLimiting;
using URLShortener.Infrastructure.Redis;

namespace URLShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Expose DbContext as the base type for Application services
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, TokenService>();

        // Redis
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        if (redisOptions.Enabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var config = ConfigurationOptions.Parse(redisOptions.ConnectionString!);
                config.AbortOnConnectFail = false; // Degrade gracefully instead of failing startup
                return ConnectionMultiplexer.Connect(config);
            });

            services.AddSingleton<IRedirectCache, RedisRedirectCache>();
        }
        else
        {
            // No Redis configured: cache reads are misses, writes are no-ops.
            services.AddSingleton<IRedirectCache, NullRedirectCache>();
        }

        // Expiration cleanup worker (Phase 4)
        services.AddHostedService<UrlExpirationCleanupWorker>();

        // Health checks: readiness depends on SQL + Redis; liveness has no dependencies.
        services.AddSingleton<SqlServerHealthCheck>();
        services.AddSingleton<RedisHealthCheck>();
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: new[] { "ready" })
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

        // Distributed rate limiting (Phase 6): counters live in Redis so limits
        // hold across every API instance behind a load balancer.
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        services.AddRateLimiter();
        services.AddOptions<RateLimiterOptions>()
            .Configure<IServiceProvider>((limiterOptions, sp) =>
            {
                var rlOptions = sp.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var redisOpts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiterOptions.OnRejected = async (context, ct) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
                        && retryAfter > TimeSpan.Zero)
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                    }

                    context.HttpContext.Response.ContentType = "application/problem+json";
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        type = "https://httpstatuses.io/429",
                        title = "Too Many Requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "Rate limit exceeded. Try again later."
                    }, ct);
                };

                RedisRateLimitStore? store = null;
                if (rlOptions.Enabled && redisOpts.Enabled)
                {
                    var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
                    store = new RedisRateLimitStore(multiplexer);
                }

                foreach (var policyName in RateLimitPolicies.All)
                {
                    if (store is null || !rlOptions.Policies.TryGetValue(policyName, out var policy))
                    {
                        // Rate limiting disabled or Redis unavailable at startup: allow all traffic.
                        limiterOptions.AddPolicy(policyName,
                            httpContext => RateLimitPartition.GetNoLimiter(httpContext.Request.Path.Value ?? "none"));
                    }
                    else
                    {
                        limiterOptions.AddPolicy(policyName, RedisSlidingWindowPartitioner.Create(
                            policyName,
                            policy,
                            store,
                            rlOptions.FailOpenOnStoreFailure,
                            loggerFactory.CreateLogger("URLShortener.Infrastructure.RateLimiting")));
                    }
                }
            });

        return services;
    }
}

/// <summary>
/// No-op cache implementation when Redis is not configured.
/// All reads are misses, all writes are ignored.
/// </summary>
internal class NullRedirectCache : IRedirectCache
{
    public Task<CachedUrlDestination?> GetAsync(string shortCode, CancellationToken cancellationToken = default)
        => Task.FromResult<CachedUrlDestination?>(null);

    public Task SetAsync(string shortCode, CachedUrlDestination destination, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetNotFoundAsync(string shortCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string shortCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
