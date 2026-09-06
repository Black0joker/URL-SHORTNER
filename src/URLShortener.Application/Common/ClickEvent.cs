namespace URLShortener.Application.Common;

/// <summary>
/// Analytics click event produced on every successful redirect.
/// Published fire-and-forget (never blocks the redirect) and persisted only
/// in aggregated form by the background worker. No raw IP addresses are retained.
/// </summary>
public sealed record ClickEvent
{
    public required Guid UrlId { get; init; }
    public required DateTime TimestampUtc { get; init; }

    /// <summary>Device class: mobile / tablet / desktop / unknown.</summary>
    public required string DeviceType { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code when known; null otherwise.</summary>
    public string? Country { get; init; }

    /// <summary>Browser family when detectable from the User-Agent.</summary>
    public string? Browser { get; init; }

    /// <summary>Host portion of the Referer header, when present.</summary>
    public string? ReferrerHost { get; init; }
}
