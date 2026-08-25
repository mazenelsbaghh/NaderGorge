using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public sealed record WhatsAppOutboundMediaSource(
    LiveSupportMessageType MessageType,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record WhatsAppOutboundMedia(
    string MediaType,
    string FileName,
    string ContentType,
    byte[] Content);

public interface IWhatsAppOutboundMediaNormalizer
{
    Task<WhatsAppOutboundMedia> NormalizeAsync(
        WhatsAppOutboundMediaSource source,
        CancellationToken cancellationToken);
}

public sealed class WhatsAppMediaNormalizationException : Exception
{
    public WhatsAppMediaNormalizationException(
        string errorCode,
        int statusCode,
        bool isRetryable,
        string message)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
    public bool IsRetryable { get; }
}
