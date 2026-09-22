using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportAttachmentStorage : ILiveSupportAttachmentStorage
{
    private readonly ISharedFileStorage _sharedStorage;

    public LiveSupportAttachmentStorage(ISharedFileStorage sharedStorage) =>
        _sharedStorage = sharedStorage;

    public async Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct)
    {
        if (sizeBytes <= 0 || sizeBytes > LiveSupportAttachmentLimits.MaximumBytes(contentType))
        {
            throw new InvalidUploadContentException("Attachment size is outside the allowed range.");
        }

        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return await SavePdfAsync(content, fileName, contentType, sizeBytes, ct);

        await using var memory = new MemoryStream();
        await content.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        var validation = UploadFileSafety.Validate(bytes, fileName, contentType, SafeUploadKind.PrivateAttachment);
        if (validation.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                bytes = await ConvertToWebpAsync(bytes, ct);
                validation = validation with
                {
                    DisplayFileName = Path.GetFileNameWithoutExtension(validation.DisplayFileName) + ".webp",
                    ContentType = "image/webp"
                };
            }
            catch (ImageFormatException)
            {
                throw new InvalidUploadContentException("Uploaded image content is invalid.");
            }
        }
        var path = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}";
        await using var input = new MemoryStream(bytes, writable: false);
        var stored = await _sharedStorage.WriteAsync(SharedFileArea.LiveSupport, path, input, ct);
        return new(path, validation.DisplayFileName, validation.ContentType, stored.SizeBytes, stored.Sha256);
    }

    private async Task<LiveSupportStoredAttachment> SavePdfAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct)
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"support-pdf-{Guid.NewGuid():N}.tmp");
        await using var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var buffer = new byte[128 * 1024];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > sizeBytes) throw new InvalidUploadContentException("Attachment length does not match.");
            await temporary.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (total != sizeBytes) throw new InvalidUploadContentException("Attachment length does not match.");
        temporary.Position = 0;
        var prefix = new byte[16];
        var prefixLength = await temporary.ReadAsync(prefix, ct);
        var validation = UploadFileSafety.Validate(prefix.AsSpan(0, prefixLength), fileName, contentType, SafeUploadKind.PrivateAttachment);
        temporary.Position = 0;
        var path = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}";
        var stored = await _sharedStorage.WriteAsync(SharedFileArea.LiveSupport, path, temporary, ct);
        return new(path, validation.DisplayFileName, validation.ContentType, stored.SizeBytes, stored.Sha256);
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) =>
        _sharedStorage.OpenReadAsync(SharedFileArea.LiveSupport, storagePath, ct);

    public Task DeleteAsync(string storagePath, CancellationToken ct) =>
        _sharedStorage.DeleteAsync(SharedFileArea.LiveSupport, storagePath, ct);

    private static async Task<byte[]> ConvertToWebpAsync(byte[] sourceBytes, CancellationToken ct)
    {
        using var image = Image.Load(sourceBytes);
        if (image.Width > 2048 || image.Height > 2048)
            image.Mutate(context => context.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(2048, 2048) }));
        await using var webp = new MemoryStream();
        await image.SaveAsWebpAsync(webp, new WebpEncoder { Quality = 82 }, ct);
        return webp.ToArray();
    }
}
