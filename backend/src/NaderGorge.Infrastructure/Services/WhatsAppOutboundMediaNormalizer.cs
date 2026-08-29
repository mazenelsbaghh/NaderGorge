using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace NaderGorge.Infrastructure.Services;

public sealed class WhatsAppOutboundMediaNormalizer(IWhatsAppAudioProcess audioProcess)
    : IWhatsAppOutboundMediaNormalizer
{
    private const int MaximumStoredBytes = 10 * 1024 * 1024;
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private const int MaximumAudioBytes = 16 * 1024 * 1024;
    private const int MaximumImageDimension = 2048;
    private const long MaximumImagePixels = 40_000_000;
    private static readonly ImageEncodingAttempt[] ImageEncodingAttempts =
    [
        new(2_048, 82),
        new(2_048, 70),
        new(1_792, 70),
        new(1_536, 65),
        new(1_280, 60),
        new(1_024, 55)
    ];
    private static readonly HashSet<string> SupportedAudioTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg", "audio/mp4", "audio/ogg", "audio/opus", "audio/webm"
    };

    public async Task<WhatsAppOutboundMedia> NormalizeAsync(
        WhatsAppOutboundMediaSource source,
        CancellationToken cancellationToken)
    {
        if (source.SizeBytes is <= 0 or > MaximumStoredBytes)
            throw Failure("WHATSAPP_MEDIA_SOURCE_SIZE_INVALID", 413,
                "The stored WhatsApp media size is not supported.");
        var sourceBytes = await ReadBoundedAsync(source.Content, MaximumStoredBytes, cancellationToken);
        if (sourceBytes.Length == 0)
            throw Failure("WHATSAPP_MEDIA_EMPTY", 422, "The stored WhatsApp media is empty.");

        return source.MessageType switch
        {
            LiveSupportMessageType.Image => await NormalizeImageAsync(source, sourceBytes, cancellationToken),
            LiveSupportMessageType.Audio => await NormalizeAudioAsync(source, sourceBytes, cancellationToken),
            _ => throw Failure("WHATSAPP_MEDIA_UNSUPPORTED", 422,
                "The stored media type is not supported by WhatsApp.")
        };
    }

    private static async Task<WhatsAppOutboundMedia> NormalizeImageAsync(
        WhatsAppOutboundMediaSource source,
        byte[] sourceBytes,
        CancellationToken cancellationToken)
    {
        if (!source.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw Failure("WHATSAPP_MEDIA_UNSUPPORTED", 422,
                "The stored image type is not supported by WhatsApp.");
        try
        {
            return await EncodeImageAsync(source.FileName, sourceBytes, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            throw InvalidImage();
        }
        catch (SixLabors.ImageSharp.InvalidImageContentException)
        {
            throw InvalidImage();
        }
        catch (ImageFormatException)
        {
            throw InvalidImage();
        }
    }

    private static async Task<WhatsAppOutboundMedia> EncodeImageAsync(
        string fileName,
        byte[] sourceBytes,
        CancellationToken cancellationToken)
    {
        EnsureImageDimensions(sourceBytes);
        using var image = Image.Load(sourceBytes);
        PrepareImage(image);
        var encoded = await EncodeJpegWithinLimitAsync(image, cancellationToken);
        return new("image", NormalizedFileName(fileName, "image", ".jpg"),
            "image/jpeg", encoded);
    }

    private static void EnsureImageDimensions(byte[] sourceBytes)
    {
        var imageInfo = Image.Identify(sourceBytes);
        if ((long)imageInfo.Width * imageInfo.Height > MaximumImagePixels)
            throw Failure("WHATSAPP_MEDIA_DIMENSIONS_TOO_LARGE", 422,
                "The stored image dimensions are not supported by WhatsApp.");
    }

    private static void PrepareImage(Image image)
    {
        image.Mutate(context =>
        {
            context.AutoOrient();
            if (image.Width > MaximumImageDimension || image.Height > MaximumImageDimension)
                context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaximumImageDimension, MaximumImageDimension)
                });
            context.BackgroundColor(Color.White);
        });
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.XmpProfile = null;
    }

    private static async Task<byte[]> EncodeJpegWithinLimitAsync(
        Image image,
        CancellationToken cancellationToken)
    {
        foreach (var attempt in ImageEncodingAttempts)
        {
            if (image.Width > attempt.MaximumDimension || image.Height > attempt.MaximumDimension)
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(attempt.MaximumDimension, attempt.MaximumDimension)
                }));
            await using var encoded = new MemoryStream();
            await image.SaveAsJpegAsync(encoded,
                new JpegEncoder { Quality = attempt.Quality }, cancellationToken);
            if (encoded.Length <= MaximumImageBytes) return encoded.ToArray();
        }
        throw Failure("WHATSAPP_MEDIA_TOO_LARGE", 413,
            "The normalized WhatsApp image exceeds the supported size.");
    }

    private async Task<WhatsAppOutboundMedia> NormalizeAudioAsync(
        WhatsAppOutboundMediaSource source,
        byte[] sourceBytes,
        CancellationToken cancellationToken)
    {
        var contentType = source.ContentType.Split(';', 2)[0].Trim();
        if (!SupportedAudioTypes.Contains(contentType))
            throw Failure("WHATSAPP_MEDIA_UNSUPPORTED", 422,
                "The stored audio type is not supported by WhatsApp.");
        var content = await audioProcess.TranscodeToMp3MonoAsync(
            sourceBytes, MaximumAudioBytes, cancellationToken);
        if (content.Length == 0)
            throw Failure("WHATSAPP_MEDIA_TRANSCODE_FAILED", 422,
                "WhatsApp audio conversion failed.");
        if (content.Length > MaximumAudioBytes)
            throw Failure("WHATSAPP_MEDIA_TOO_LARGE", 413,
                "The normalized WhatsApp audio exceeds the supported size.");
        return new("audio", NormalizedFileName(source.FileName, "audio", ".mp3"),
            "audio/mpeg", content);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[81_920];
        while (true)
        {
            var remaining = maximumBytes - content.Length;
            var bytesRead = await source.ReadAsync(
                buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining + 1)),
                cancellationToken);
            if (bytesRead == 0) return content.ToArray();
            if (bytesRead > remaining)
                throw Failure("WHATSAPP_MEDIA_SOURCE_SIZE_INVALID", 413,
                    "The stored WhatsApp media size is not supported.");
            await content.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static string NormalizedFileName(string fileName, string fallback, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName).Trim();
        baseName = new string(baseName
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or ' ')
            .Take(80)
            .ToArray())
            .Trim();
        if (string.IsNullOrWhiteSpace(baseName)) baseName = fallback;
        return $"{baseName}{extension}";
    }

    private static WhatsAppMediaNormalizationException InvalidImage() =>
        Failure("WHATSAPP_MEDIA_INVALID_IMAGE", 422,
            "The stored image could not be prepared for WhatsApp.");

    private static WhatsAppMediaNormalizationException Failure(
        string errorCode,
        int statusCode,
        string message) =>
        new(errorCode, statusCode, false, message);

    private sealed record ImageEncodingAttempt(int MaximumDimension, int Quality);
}
