namespace URLShortener.Domain.Entities;

/// <summary>
/// Aggregated daily click counter for a URL. Composite key (UrlId, Date).
/// Written in batches by the analytics worker; never on the redirect hot path.
/// </summary>
public class UrlDailyStat
{
    public Guid UrlId { get; set; }
    public DateOnly Date { get; set; }
    public long ClickCount { get; set; }
}
