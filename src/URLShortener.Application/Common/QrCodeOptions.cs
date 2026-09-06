namespace URLShortener.Application.Common;

public class QrCodeOptions
{
    public const string SectionName = "QrCode";

    /// <summary>Pixels per QR module (scale factor) for rendered images.</summary>
    public int PixelsPerModule { get; init; } = 10;

    /// <summary>TTL for cached QR images. QR content is deterministic per short code.</summary>
    public int CacheTtlSeconds { get; init; } = 7 * 24 * 3600;
}
