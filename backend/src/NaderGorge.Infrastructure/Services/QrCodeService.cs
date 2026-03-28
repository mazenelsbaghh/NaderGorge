using QRCoder;

namespace NaderGorge.Infrastructure.Services;

/// <summary>
/// Generates QR code images for access codes.
/// QR data format: {baseUrl}/qr/{codeHash}
/// </summary>
public class QrCodeService
{
    private readonly string _baseUrl;

    public QrCodeService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Generate a single QR code as PNG bytes for the given code hash.
    /// </summary>
    public byte[] GenerateQrPng(string codeHash, int pixelsPerModule = 10)
    {
        var url = $"{_baseUrl}/qr/{codeHash}";

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Generate QR codes for multiple codes and return as a ZIP archive stream.
    /// Each QR is named by the code's plaintext value.
    /// </summary>
    public MemoryStream GenerateQrZip(IEnumerable<(string CodePlaintext, string CodeHash)> codes, int pixelsPerModule = 8)
    {
        var zipStream = new MemoryStream();

        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (plaintext, hash) in codes)
            {
                var pngBytes = GenerateQrPng(hash, pixelsPerModule);
                var entry = archive.CreateEntry($"{plaintext}.png", System.IO.Compression.CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(pngBytes, 0, pngBytes.Length);
            }
        }

        zipStream.Position = 0;
        return zipStream;
    }
}
