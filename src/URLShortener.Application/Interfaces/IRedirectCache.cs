using URLShortener.Application.Common;

namespace URLShortener.Application.Interfaces;

/// <summary>
/// Cache-aside store for the hot redirect path (short code -> destination).
/// Implementations must degrade gracefully when the backing store (e.g. Redis)
/// is unavailable: reads behave as cache misses, writes become no-ops.
/// </summary>
public interface IRedirectCache
{
    /// <summary>
    /// Returns null on cache miss, a negative entry (<see cref="CachedUrlDestination.Exists"/> == false),
    /// or the cached destination.
    /// </summary>
    Task<CachedUrlDestination?> GetAsync(string shortCode, CancellationToken cancellationToken = default);

    /// <summary>Stores the destination with a TTL derived from configuration and expiration.</summary>
    Task SetAsync(string shortCode, CachedUrlDestination destination, CancellationToken cancellationToken = default);

    /// <summary>Stores a short-lived negative entry protecting SQL from repeated invalid lookups.</summary>
    Task SetNotFoundAsync(string shortCode, CancellationToken cancellationToken = default);

    /// <summary>Invalidates the entry after the URL is updated or deleted (SQL stays authoritative).</summary>
    Task RemoveAsync(string shortCode, CancellationToken cancellationToken = default);
}
