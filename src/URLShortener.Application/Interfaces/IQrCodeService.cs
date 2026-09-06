using URLShortener.Application.DTOs;

namespace URLShortener.Application.Interfaces;

/// <summary>Generates (or serves from cache) the QR code for a user-owned URL.</summary>
public interface IQrCodeService
{
    /// <summary>
    /// Returns the QR image for the URL, or null when the URL does not exist
    /// or is not owned by <paramref name="userId"/>.
    /// </summary>
    /// <param name="format">"png" or "svg"; anything else defaults to png.</param>
    Task<QrCodeImage?> GetQrCodeAsync(Guid urlId, Guid userId, string? format, CancellationToken cancellationToken = default);
}
