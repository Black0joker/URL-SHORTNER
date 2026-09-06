namespace URLShortener.Application.Common;

/// <summary>
/// The value stored in the redirect cache for a short code.
/// When <see cref="Exists"/> is false this represents a negative-cache entry
/// (the short code is known not to exist).
/// </summary>
public sealed record CachedUrlDestination
{
    /// <summary>Database identity of the URL; used for analytics attribution. Null for legacy entries.</summary>
    public Guid? UrlId { get; init; }
    public bool Exists { get; init; }
    public string? OriginalUrl { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }

    public static CachedUrlDestination NotFound { get; } = new()
    {
        Exists = false,
        OriginalUrl = null,
        ExpiresAt = null,
        IsActive = false
    };

    public static CachedUrlDestination For(Guid urlId, string originalUrl, DateTime? expiresAt, bool isActive) => new()
    {
        UrlId = urlId,
        Exists = true,
        OriginalUrl = originalUrl,
        ExpiresAt = expiresAt,
        IsActive = isActive
    };
}
