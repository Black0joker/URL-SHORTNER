namespace URLShortener.Infrastructure.Analytics;

/// <summary>Configuration for the asynchronous click-analytics pipeline.</summary>
public class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    /// <summary>Redis stream key holding raw click events.</summary>
    public string StreamKey { get; init; } = "analytics:clicks";

    /// <summary>Consumer group shared by every API instance's aggregation worker.</summary>
    public string ConsumerGroup { get; init; } = "workers";

    /// <summary>Events read and aggregated per worker iteration.</summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>Approximate MAXLEN cap for the stream (old events trimmed).</summary>
    public int MaxStreamLength { get; init; } = 100_000;

    /// <summary>Capacity of the in-process publish buffer between redirect and Redis.</summary>
    public int PublishBufferCapacity { get; init; } = 10_000;

    /// <summary>Pause after a failed Redis flush before retrying.</summary>
    public int FlushFailureDelayMilliseconds { get; init; } = 1_000;

    /// <summary>Poll interval when the stream has no new messages.</summary>
    public int PollDelayMilliseconds { get; init; } = 500;
}
