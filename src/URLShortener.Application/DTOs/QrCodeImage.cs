namespace URLShortener.Application.DTOs;

/// <summary>A rendered QR image ready to be served as an HTTP file response.</summary>
/// <param name="ContentType">MIME type, e.g. image/png or image/svg+xml.</param>
/// <param name="Content">Encoded image bytes.</param>
public sealed record QrCodeImage(string ContentType, byte[] Content);
