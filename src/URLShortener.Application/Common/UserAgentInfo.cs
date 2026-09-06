namespace URLShortener.Application.Common;

/// <summary>
/// Lightweight User-Agent classification for analytics.
/// Pure string parsing; no external dependencies.
/// </summary>
public static class UserAgentInfo
{
    public const string Mobile = "mobile";
    public const string Tablet = "tablet";
    public const string Desktop = "desktop";
    public const string Unknown = "unknown";

    /// <summary>Classifies the User-Agent into a coarse device bucket.</summary>
    public static string ParseDeviceType(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return Unknown;

        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Kindle", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Silk", StringComparison.OrdinalIgnoreCase))
            return Tablet;

        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Windows Phone", StringComparison.OrdinalIgnoreCase))
            return Mobile;

        if (userAgent.Contains("Mozilla", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Windows NT", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            return Desktop;

        return Unknown;
    }

    /// <summary>Detects the browser family, or null when it cannot be determined.</summary>
    public static string? ParseBrowser(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return null;

        // Order matters: Edge/Firefox UA strings also contain other tokens.
        if (userAgent.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            return "edge";
        if (userAgent.Contains("OPR", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
            return "opera";
        if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("FxiOS", StringComparison.OrdinalIgnoreCase))
            return "firefox";
        if (userAgent.Contains("CriOS", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            return "chrome";
        if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase))
            return "safari";

        return null;
    }
}
