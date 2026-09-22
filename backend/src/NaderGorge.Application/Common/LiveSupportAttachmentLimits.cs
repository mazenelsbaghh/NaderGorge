namespace NaderGorge.Application.Common;

public static class LiveSupportAttachmentLimits
{
    public const long PdfBytes = 90L * 1024 * 1024;
    public const long OtherBytes = 10L * 1024 * 1024;
    public const long RequestBytes = PdfBytes + 1024 * 1024;

    public static long MaximumBytes(string contentType) =>
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase) ? PdfBytes : OtherBytes;
}
