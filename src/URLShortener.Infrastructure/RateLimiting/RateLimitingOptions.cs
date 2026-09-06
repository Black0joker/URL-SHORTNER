namespace URLShortener.Infrastructure.RateLimiting;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Master switch. When false, all policies allow every request.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// When the Redis store is unreachable: true = allow requests (availability first),
    /// false = reject with 429 (strict enforcement).
    /// </summary>
    public bool FailOpenOnStoreFailure { get; init; } = true;

    public Dictionary<string, RateLimitPolicyOptions> Policies { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public class RateLimitPolicyOptions
{
    /// <summary>Maximum number of requests allowed per sliding window.</summary>
    public int PermitLimit { get; init; }

    /// <summary>Window size in seconds.</summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>"ip" partitions by client address; "user" partitions by authenticated user id (falls back to IP).</summary>
    public string PartitionBy { get; init; } = "ip";
}
