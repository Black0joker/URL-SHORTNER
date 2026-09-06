namespace URLShortener.Application.Interfaces;

/// <summary>
/// Binary cache for deterministic QR images. Redis-backed when Redis is enabled,
/// no-op otherwise (QR generation then happens per request).
/// </summary>
public interface IQrCodeCache
{
    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, byte[] content, TimeSpan ttl, CancellationToken cancellationToken = default);
}
