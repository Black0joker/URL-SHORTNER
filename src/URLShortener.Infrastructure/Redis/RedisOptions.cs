namespace URLShortener.Infrastructure.Redis;

public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string, e.g. "localhost:6379".</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Default TTL for cached redirect entries.</summary>
    public int EntryTtlSeconds { get; init; } = 3600;

    /// <summary>TTL for negative (not-found) cache entries.</summary>
    public int NegativeTtlSeconds { get; init; } = 30;

    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);
}
