using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using URLShortener.Application.Common;
using URLShortener.Application.Interfaces;
using URLShortener.Domain.Entities;

namespace URLShortener.Application.Services;

/// <summary>
/// Cache-aside redirect resolution.
/// Flow: Redis -> (hit) redirect | (miss) SQL Server -> populate Redis -> redirect.
/// Uses per-short-code single-flight coalescing to protect SQL Server from
/// cache-stampede bursts on hot keys.
/// </summary>
public class RedirectService : IRedirectService
{
    private readonly IRedirectCache _cache;
    private readonly DbContext _dbContext;
    private readonly ILogger<RedirectService> _logger;

    // Single-flight map: only one in-flight DB load per short code.
    private readonly ConcurrentDictionary<string, Task<ResolvedRedirect?>> _inFlight = new();

    public RedirectService(
        IRedirectCache cache,
        DbContext dbContext,
        ILogger<RedirectService> logger)
    {
        _cache = cache;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ResolvedRedirect?> ResolveAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        // 1) Cache lookup.
        var cached = await _cache.GetAsync(shortCode, cancellationToken);

        if (cached is not null)
            return Evaluate(cached);

        // 2) Cache miss -> single-flight load from SQL Server, then populate cache.
        var task = _inFlight.GetOrAdd(shortCode, code => LoadAndCacheAsync(code, cancellationToken));

        try
        {
            return await task;
        }
        finally
        {
            // Remove the coalescing entry once the shared task completes so future
            // misses re-query. Compare against our task to avoid removing a newer one.
            _inFlight.TryRemove(new KeyValuePair<string, Task<ResolvedRedirect?>>(shortCode, task));
        }
    }

    private async Task<ResolvedRedirect?> LoadAndCacheAsync(string shortCode, CancellationToken cancellationToken)
    {
        var url = await _dbContext.Set<Url>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode && u.DeletedAt == null, cancellationToken);

        if (url is null || !url.IsActive)
        {
            // Negative caching: remember the miss briefly to shield SQL Server.
            await _cache.SetNotFoundAsync(shortCode, cancellationToken);
            return null;
        }

        var destination = CachedUrlDestination.For(url.Id, url.OriginalUrl, url.ExpiresAt, url.IsActive);
        await _cache.SetAsync(shortCode, destination, cancellationToken);

        return Evaluate(destination);
    }

    /// <summary>
    /// Application-level expiration/activation check. SQL remains the source of truth;
    /// this only decides whether a cached entry is currently servable.
    /// </summary>
    private static ResolvedRedirect? Evaluate(CachedUrlDestination destination)
    {
        if (!destination.Exists || !destination.IsActive)
            return null;

        if (destination.ExpiresAt.HasValue && destination.ExpiresAt.Value <= DateTime.UtcNow)
            return null;

        if (destination.UrlId is null || destination.OriginalUrl is null)
            return null;

        return new ResolvedRedirect(destination.UrlId.Value, destination.OriginalUrl);
    }
}
