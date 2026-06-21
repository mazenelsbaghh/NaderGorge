using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAttachment : BaseEntity
{
    public string StoragePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string UploadedByIdentity { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
}
