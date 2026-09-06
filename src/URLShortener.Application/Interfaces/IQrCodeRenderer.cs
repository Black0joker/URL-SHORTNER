namespace URLShortener.Application.Interfaces;

/// <summary>Renders QR images. Implemented by a third-party QR library adapter in Infrastructure.</summary>
public interface IQrCodeRenderer
{
    /// <summary>Renders the content as a PNG image.</summary>
    byte[] RenderPng(string content, int pixelsPerModule);

    /// <summary>Renders the content as an SVG document.</summary>
    string RenderSvg(string content, int pixelsPerModule);
}
