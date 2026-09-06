using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using URLShortener.Application.Interfaces;

namespace URLShortener.Infrastructure.Redis;

/// <summary>
/// Redis-backed binary cache for deterministic QR images.
/// Degrades to miss/no-op when Redis is unavailable, exactly like the redirect cache.
/// </summary>
public class RedisQrCodeCache : IQrCodeCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisQrCodeCache> _logger;

    public RedisQrCodeCache(IConnectionMultiplexer redis, ILogger<RedisQrCodeCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            return value.IsNullOrEmpty ? null : (byte[]?)value;
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning("Redis unavailable during QR GET for {Key}; regenerating.", key);
            return null;
        }
    }

    public async Task SetAsync(string key, byte[] content, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, content, ttl);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning("Redis unavailable during QR SET for {Key}; skipping cache write.", key);
        }
    }

    private static bool IsRedisFailure(Exception ex) =>
        ex is RedisException or RedisConnectionException or RedisTimeoutException or TimeoutException or IOException;
}
