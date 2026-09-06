namespace URLShortener.Domain.Entities;

/// <summary>
/// Aggregated daily click counter per country (ISO 3166-1 alpha-2).
/// Composite key (UrlId, Date, CountryCode).
/// </summary>
public class UrlCountryDailyStat
{
    public Guid UrlId { get; set; }
    public DateOnly Date { get; set; }
    public required string CountryCode { get; set; }
    public long ClickCount { get; set; }
}
