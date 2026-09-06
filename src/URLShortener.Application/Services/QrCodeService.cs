using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using URLShortener.Application.Common;
using URLShortener.Application.DTOs;
using URLShortener.Application.Interfaces;
using URLShortener.Domain.Entities;

namespace URLShortener.Application.Services;

/// <summary>
/// Serves QR codes for user-owned URLs. The QR payload is the short URL itself,
/// which is deterministic, so rendered images are cached in Redis and reused.
/// </summary>
public class QrCodeService : IQrCodeService
{
    private const string SvgFormat = "svg";
    private const string PngContentType = "image/png";
    private const string SvgContentType = "image/svg+xml";

    private readonly DbContext _dbContext;
    private readonly IQrCodeRenderer _renderer;
    private readonly IQrCodeCache _cache;
    private readonly QrCodeOptions _options;
    private readonly string _baseUrl;

    public QrCodeService(
        DbContext dbContext,
        IQrCodeRenderer renderer,
        IQrCodeCache cache,
        IOptions<QrCodeOptions> qrOptions,
        IOptions<UrlShorteningOptions> urlOptions)
    {
        _dbContext = dbContext;
        _renderer = renderer;
        _cache = cache;
        _options = qrOptions.Value;
        _baseUrl = urlOptions.Value.BaseUrl.TrimEnd('/');
    }

    public async Task<QrCodeImage?> GetQrCodeAsync(Guid urlId, Guid userId, string? format, CancellationToken cancellationToken = default)
    {
        var normalizedFormat = string.Equals(format, SvgFormat, StringComparison.OrdinalIgnoreCase) ? SvgFormat : "png";

        // Ownership check: QR codes are exposed through the same authorized surface as URLs.
        var url = await _dbContext.Set<Url>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == urlId && u.UserId == userId && u.DeletedAt == null, cancellationToken);

        if (url is null)
            return null;

        var cacheKey = $"qr:{url.ShortCode}:{normalizedFormat}";
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);

        if (cached is not null)
            return new QrCodeImage(ContentTypeFor(normalizedFormat), cached);

        // The QR content is the short URL — stable for the lifetime of the short code.
        var content = $"{_baseUrl}/{url.ShortCode}";

        var bytes = normalizedFormat == SvgFormat
            ? Encoding.UTF8.GetBytes(_renderer.RenderSvg(content, _options.PixelsPerModule))
            : _renderer.RenderPng(content, _options.PixelsPerModule);

        await _cache.SetAsync(cacheKey, bytes, TimeSpan.FromSeconds(_options.CacheTtlSeconds), cancellationToken);

        return new QrCodeImage(ContentTypeFor(normalizedFormat), bytes);
    }

    private static string ContentTypeFor(string format) => format == SvgFormat ? SvgContentType : PngContentType;
}
