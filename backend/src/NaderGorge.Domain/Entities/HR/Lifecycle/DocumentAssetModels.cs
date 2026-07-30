using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class EmployeeDocument : BaseEntity
{
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public EmployeeDocumentCategory Category { get; set; } public string Name { get; set; } = string.Empty;
    public DateOnly? IssuedOn { get; set; } public DateOnly? ExpiresOn { get; set; } public DateOnly? RetainUntil { get; set; }
    public bool LegalHold { get; set; } public bool IsArchived { get; set; }
    public ICollection<EmployeeDocumentVersion> Versions { get; set; } = new List<EmployeeDocumentVersion>();
}

public sealed class EmployeeDocumentVersion : BaseEntity
{
    public Guid EmployeeDocumentId { get; set; } public EmployeeDocument? EmployeeDocument { get; set; }
    public int Version { get; set; } = 1; public string AssetReference { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty; public string MimeType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; } public Guid UploadedByUserId { get; set; }
}

public sealed class HrAsset : BaseEntity
{
    public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string? SerialNumber { get; set; }
    public decimal Value { get; set; } public HrAssetStatus Status { get; set; } = HrAssetStatus.Available;
    public ICollection<AssetCustody> Custodies { get; set; } = new List<AssetCustody>();
}

public sealed class AssetCustody : BaseEntity
{
    public Guid AssetId { get; set; } public HrAsset? Asset { get; set; } public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public DateTime AssignedAt { get; set; } public Guid AssignedByUserId { get; set; } public string AssignedCondition { get; set; } = string.Empty;
    public AssetCustodyState State { get; set; } = AssetCustodyState.Active; public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; } public Guid? ClosedByUserId { get; set; }
    public Guid? ExceptionApprovedByUserId { get; set; } public string? ExceptionReason { get; set; }
}
