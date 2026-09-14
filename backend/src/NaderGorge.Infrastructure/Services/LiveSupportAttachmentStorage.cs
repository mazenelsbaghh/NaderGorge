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
        if (sizeBytes is <= 0 or > 10 * 1024 * 1024)
        {
            throw new InvalidUploadContentException("Attachment size is outside the allowed range.");
        }

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
