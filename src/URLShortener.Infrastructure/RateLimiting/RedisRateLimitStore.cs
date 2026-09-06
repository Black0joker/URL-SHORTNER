using StackExchange.Redis;

namespace URLShortener.Infrastructure.RateLimiting;

/// <summary>
/// Distributed sliding-window rate limit counters stored in Redis.
/// The state lives in Redis so every API instance enforces the same budget;
/// no in-process counter can be bypassed by spreading requests across instances.
///
/// Algorithm: sliding-window counter approximation over two fixed windows
/// (previous window weighted by the overlap), evaluated atomically in Lua.
/// </summary>
public sealed class RedisRateLimitStore
{
    private const string KeyPrefix = "rl:";

    // Atomic sliding-window counter.
    // KEYS[1] = partition key
    // ARGV: now (ms), window (ms), limit, permits requested
    // Returns: { allowed (0/1), retryAfterMs }
    private const string SlidingWindowScript = """
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local limit = tonumber(ARGV[3])
        local permits = tonumber(ARGV[4])

        local currStart = now - (now % window)
        local currKey = key .. ':' .. tostring(currStart)
        local prevKey = key .. ':' .. tostring(currStart - window)

        local prevCount = tonumber(redis.call('GET', prevKey) or '0')
        local currCount = tonumber(redis.call('GET', currKey) or '0')

        local elapsed = now - currStart
        local estimated = prevCount * (1 - elapsed / window) + currCount

        if estimated + permits > limit then
            return { 0, window - elapsed }
        end

        redis.call('INCRBY', currKey, permits)
        redis.call('PEXPIRE', currKey, window * 2)
        return { 1, 0 }
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisRateLimitStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<(bool Allowed, TimeSpan RetryAfter)> TryAcquireAsync(
        string partitionKey, int permitLimit, TimeSpan window, int permits = 1, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            SlidingWindowScript,
            [new RedisKey($"{KeyPrefix}{partitionKey}")],
            BuildArgs(permitLimit, window, permits));

        return ParseResult(result);
    }

    public (bool Allowed, TimeSpan RetryAfter) TryAcquire(
        string partitionKey, int permitLimit, TimeSpan window, int permits = 1)
    {
        var db = _redis.GetDatabase();
        var result = db.ScriptEvaluate(
            SlidingWindowScript,
            [new RedisKey($"{KeyPrefix}{partitionKey}")],
            BuildArgs(permitLimit, window, permits));

        return ParseResult(result);
    }

    private static RedisValue[] BuildArgs(int permitLimit, TimeSpan window, int permits) =>
    [
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        (long)window.TotalMilliseconds,
        permitLimit,
        permits
    ];

    private static (bool Allowed, TimeSpan RetryAfter) ParseResult(RedisResult result)
    {
        var parts = (RedisResult[]?)result
            ?? throw new InvalidOperationException("Unexpected null response from rate limit script.");

        if (parts.Length < 2)
            throw new InvalidOperationException("Unexpected response from rate limit script.");

        var allowed = (long)parts[0] == 1;
        var retryAfterMs = (long)parts[1];

        return (allowed, TimeSpan.FromMilliseconds(Math.Max(0, retryAfterMs)));
    }
}
