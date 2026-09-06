using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using URLShortener.Application.Common;
using URLShortener.Application.Interfaces;

namespace URLShortener.Infrastructure.Redis;

/// <summary>
/// Redis-backed cache-aside store for the redirect hot path.
/// Degrades gracefully to "cache miss / no-op" when Redis is unavailable so
/// the API can fall back to SQL Server instead of failing requests.
/// </summary>
public class RedisRedirectCache : IRedirectCache
{
    private const string KeyPrefix = "url:";
    private const string NotFoundMarker = "__NOT_FOUND__";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisRedirectCache> _logger;

    public RedisRedirectCache(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisRedirectCache> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CachedUrlDestination?> GetAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(BuildKey(shortCode));

            if (value.IsNullOrEmpty)
                return null; // cache miss

            var text = value.ToString();
            if (text == NotFoundMarker)
                return CachedUrlDestination.NotFound;

            return JsonSerializer.Deserialize<CachedUrlDestination>(text, JsonOptions);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning("Redis unavailable during GET for {ShortCode}; treating as cache miss.", shortCode);
            return null;
        }
    }

    public async Task SetAsync(string shortCode, CachedUrlDestination destination, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var ttl = ComputeTtl(destination.ExpiresAt);
            var payload = JsonSerializer.Serialize(destination, JsonOptions);

            await db.StringSetAsync(BuildKey(shortCode), payload, ttl);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning("Redis unavailable during SET for {ShortCode}; skipping cache write.", shortCode);
        }
    }

    public async Task SetNotFoundAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var ttl = TimeSpan.FromSeconds(_options.NegativeTtlSeconds);

            await db.StringSetAsync(BuildKey(shortCode), NotFoundMarker, ttl);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning("Redis unavailable during negative SET for {ShortCode}; skipping.", shortCode);
        }
    }

    public async Task RemoveAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(BuildKey(shortCode));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning("Redis unavailable during DEL for {ShortCode}; cache may serve stale until TTL.", shortCode);
        }
    }

    private static string BuildKey(string shortCode) => $"{KeyPrefix}{shortCode}";

    /// <summary>
    /// TTL is the configured entry TTL, but never outlives the URL's own expiration.
    /// Redis TTL is a cache mechanism only; SQL remains the source of truth.
    /// </summary>
    private TimeSpan ComputeTtl(DateTime? expiresAt)
    {
        var configured = TimeSpan.FromSeconds(_options.EntryTtlSeconds);

        if (expiresAt is null)
            return configured;

        var untilExpiry = expiresAt.Value - DateTime.UtcNow;
        if (untilExpiry <= TimeSpan.Zero)
            return TimeSpan.FromSeconds(1); // already expired; drop almost immediately

        return untilExpiry < configured ? untilExpiry : configured;
    }

    private static bool IsRedisFailure(Exception ex) =>
        ex is RedisException or RedisConnectionException or RedisTimeoutException or TimeoutException or IOException;
}
