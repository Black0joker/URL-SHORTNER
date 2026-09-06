namespace URLShortener.Application.DTOs;

/// <summary>Aggregated click statistics for one URL over a date window.</summary>
public class UrlStatsResponse
{
    public Guid UrlId { get; init; }
    public required string ShortCode { get; init; }
    public DateOnly FromDate { get; init; }
    public long TotalClicks { get; init; }

    /// <summary>Clicks per day within the window (days with zero clicks are omitted).</summary>
    public List<DailyClickCount> Daily { get; init; } = [];

    /// <summary>Top countries by clicks within the window.</summary>
    public List<DimensionClickCount> Countries { get; init; } = [];

    /// <summary>Device-class breakdown within the window.</summary>
    public List<DimensionClickCount> Devices { get; init; } = [];
}

public class DailyClickCount
{
    public DateOnly Date { get; init; }
    public long ClickCount { get; init; }
}

public class DimensionClickCount
{
    public required string Value { get; init; }
    public long ClickCount { get; init; }
}
