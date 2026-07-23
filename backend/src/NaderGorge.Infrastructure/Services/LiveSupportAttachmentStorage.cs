using System.Security.Cryptography;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportAttachmentStorage : ILiveSupportAttachmentStorage
{
    private readonly string _root;
    public LiveSupportAttachmentStorage()
    {
        _root = Path.Combine(AppContext.BaseDirectory, "uploads", "live-support");
        Directory.CreateDirectory(_root);
    }

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
        var path = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}";
        var fullPath = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = File.Create(fullPath);
        using var sha = SHA256.Create();
        await using var hashing = new CryptoStream(output, sha, CryptoStreamMode.Write);
        await hashing.WriteAsync(bytes, ct);
        await hashing.FlushFinalBlockAsync(ct);
        return new(path, validation.DisplayFileName, validation.ContentType, bytes.LongLength, Convert.ToHexString(sha.Hash!));
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) => Task.FromResult<Stream>(File.OpenRead(Resolve(storagePath)));
    public Task DeleteAsync(string storagePath, CancellationToken ct) { var path = Resolve(storagePath); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
    private string Resolve(string path)
    {
        var root = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, path));
        if (!resolved.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("Invalid attachment path.");
        return resolved;
    }
}
