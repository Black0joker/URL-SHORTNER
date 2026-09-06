using QRCoder;
using URLShortener.Application.Interfaces;

namespace URLShortener.Infrastructure.QrCodes;

/// <summary>
/// QRCoder adapter. Uses the System.Drawing-free renderers (PngByteQRCode,
/// SvgQRCode) so it works on every platform.
/// </summary>
public sealed class QrCoderRenderer : IQrCodeRenderer
{
    public byte[] RenderPng(string content, int pixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    public string RenderSvg(string content, int pixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var svg = new SvgQRCode(data);
        return svg.GetGraphic(pixelsPerModule);
    }
}
