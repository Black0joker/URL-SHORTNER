using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace URLShortener.Infrastructure.RateLimiting;

/// <summary>Policy parameters for one partition (policy + identity).</summary>
internal sealed record RedisSlidingWindowMetadata(
    string PartitionKey,
    int PermitLimit,
    TimeSpan Window);

/// <summary>
/// Custom <see cref="RateLimiter"/> whose permit decisions are made atomically in Redis,
/// making the limit distributed across all API instances behind the load balancer.
/// </summary>
internal sealed class RedisSlidingWindowRateLimiter : RateLimiter
{
    private readonly RedisRateLimitStore _store;
    private readonly RedisSlidingWindowMetadata _metadata;
    private readonly bool _failOpen;
    private readonly ILogger _logger;

    public RedisSlidingWindowRateLimiter(
        RedisRateLimitStore store,
        RedisSlidingWindowMetadata metadata,
        bool failOpen,
        ILogger logger)
    {
        _store = store;
        _metadata = metadata;
        _failOpen = failOpen;
        _logger = logger;
    }

    public override TimeSpan? IdleDuration => TimeSpan.Zero;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        try
        {
            var (allowed, retryAfter) = _store.TryAcquire(
                _metadata.PartitionKey, _metadata.PermitLimit, _metadata.Window, permitCount);

            return new RedisRateLimitLease(allowed, retryAfter);
        }
        catch (Exception ex)
        {
            return OnStoreFailure(ex);
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            var (allowed, retryAfter) = await _store.TryAcquireAsync(
                _metadata.PartitionKey, _metadata.PermitLimit, _metadata.Window, permitCount, cancellationToken);

            return new RedisRateLimitLease(allowed, retryAfter);
        }
        catch (Exception ex)
        {
            return OnStoreFailure(ex);
        }
    }

    public override RateLimiterStatistics GetStatistics() => new();

    private RedisRateLimitLease OnStoreFailure(Exception ex)
    {
        // Redis is unavailable. Fail-open keeps the service available (limits are
        // temporarily unenforced); fail-closed returns 429. Both are logged.
        _logger.LogWarning(ex, "Rate limit store unavailable for {Partition}. FailOpen={FailOpen}.",
            _metadata.PartitionKey, _failOpen);

        return new RedisRateLimitLease(_failOpen, TimeSpan.Zero);
    }
}

/// <summary>Lease exposing the acquisition result and Retry-After metadata.</summary>
internal sealed class RedisRateLimitLease : RateLimitLease
{
    private static readonly string RetryAfterName = MetadataName.RetryAfter.Name;
    private static readonly string[] AllMetadataNames = [RetryAfterName];

    private readonly TimeSpan _retryAfter;

    public RedisRateLimitLease(bool isAcquired, TimeSpan retryAfter)
    {
        IsAcquired = isAcquired;
        _retryAfter = retryAfter;
    }

    public override bool IsAcquired { get; }

    public override IEnumerable<string> MetadataNames => AllMetadataNames;

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (metadataName == RetryAfterName && _retryAfter > TimeSpan.Zero)
        {
            metadata = _retryAfter;
            return true;
        }

        metadata = null;
        return false;
    }
}
