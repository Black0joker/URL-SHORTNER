namespace URLShortener.Domain.Entities;

/// <summary>
/// Aggregated daily click counter per device class (mobile/tablet/desktop/unknown).
/// Composite key (UrlId, Date, DeviceType).
/// </summary>
public class UrlDeviceDailyStat
{
    public Guid UrlId { get; set; }
    public DateOnly Date { get; set; }
    public required string DeviceType { get; set; }
    public long ClickCount { get; set; }
}
