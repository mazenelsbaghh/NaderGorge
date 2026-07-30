namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public sealed record LiveSupportStoredAttachment(string StoragePath, string OriginalFileName, string ContentType, long SizeBytes, string Sha256);

public interface ILiveSupportAttachmentStorage
{
    Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct);
    Task DeleteAsync(string storagePath, CancellationToken ct);
}
