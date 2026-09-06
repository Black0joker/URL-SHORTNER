using StackExchange.Redis;
using URLShortener.Application.Common;

namespace URLShortener.Infrastructure.Analytics;

/// <summary>
/// Encodes/decodes <see cref="ClickEvent"/> to compact Redis stream field/value pairs.
/// Field names are kept short because they are stored once per event.
/// </summary>
internal static class ClickEventCodec
{
    private const string UrlIdField = "u";
    private const string TimestampField = "t";
    private const string DeviceField = "d";
    private const string CountryField = "c";
    private const string BrowserField = "b";
    private const string ReferrerField = "r";

    public static NameValueEntry[] Encode(ClickEvent clickEvent)
    {
        var entries = new List<NameValueEntry>(6)
        {
            new(UrlIdField, clickEvent.UrlId.ToString("N")),
            new(TimestampField, new DateTimeOffset(clickEvent.TimestampUtc).ToUnixTimeMilliseconds()),
            new(DeviceField, clickEvent.DeviceType)
        };

        if (clickEvent.Country is not null)
            entries.Add(new NameValueEntry(CountryField, clickEvent.Country));

        if (clickEvent.Browser is not null)
            entries.Add(new NameValueEntry(BrowserField, clickEvent.Browser));

        if (clickEvent.ReferrerHost is not null)
            entries.Add(new NameValueEntry(ReferrerField, clickEvent.ReferrerHost));

        return entries.ToArray();
    }

    /// <summary>Decodes a stream entry; returns null for malformed entries (they are skipped).</summary>
    public static ClickEvent? Decode(NameValueEntry[] entries)
    {
        string? urlIdText = null;
        string? device = null;
        string? country = null;
        string? browser = null;
        string? referrer = null;
        long timestampMs = 0;

        foreach (var entry in entries)
        {
            var name = entry.Name.ToString();
            var value = entry.Value.ToString();

            switch (name)
            {
                case UrlIdField: urlIdText = value; break;
                case TimestampField: long.TryParse(value, out timestampMs); break;
                case DeviceField: device = value; break;
                case CountryField: country = value; break;
                case BrowserField: browser = value; break;
                case ReferrerField: referrer = value; break;
            }
        }

        if (urlIdText is null || !Guid.TryParseExact(urlIdText, "N", out var urlId))
            return null;

        return new ClickEvent
        {
            UrlId = urlId,
            TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime,
            DeviceType = string.IsNullOrWhiteSpace(device) ? UserAgentInfo.Unknown : device,
            Country = string.IsNullOrWhiteSpace(country) ? null : country,
            Browser = string.IsNullOrWhiteSpace(browser) ? null : browser,
            ReferrerHost = string.IsNullOrWhiteSpace(referrer) ? null : referrer
        };
    }
}
