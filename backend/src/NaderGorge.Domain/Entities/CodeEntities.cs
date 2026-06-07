using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class CodeGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int TotalCodes { get; set; }
    public CodeType CodeType { get; set; } = CodeType.Package;

    // --- Target references (nullable, depends on CodeType) ---
    public Guid? PackageId { get; set; }
    public Guid? TermId { get; set; }
    public Guid? ContentSectionId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? ExamId { get; set; }

    // --- Balance code fields ---
    public decimal? BalanceAmount { get; set; }

    // --- Discount & expiration ---
    public decimal? DiscountPercentage { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool QrDataGenerated { get; set; } = false;

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    // Navigation
    public ICollection<AccessCode> AccessCodes { get; set; } = new List<AccessCode>();
    public ICollection<CodeVideoTarget> CodeVideoTargets { get; set; } = new List<CodeVideoTarget>();
}

public class AccessCode : BaseEntity
{
    public string CodeHash { get; set; } = string.Empty;
    public string CodePlaintext { get; set; } = string.Empty; // stored only during generation, cleared after export
    public Guid CodeGroupId { get; set; }
    public CodeGroup CodeGroup { get; set; } = null!;

    public bool IsConsumed { get; set; } = false;
    public Guid? ConsumedByUserId { get; set; }
    public User? ConsumedByUser { get; set; }
    public DateTime? ConsumedAt { get; set; }

    // --- New fields ---
    public string? QrCodeUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long SerialNumber { get; set; }
}

public class StudentAccessGrant : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // --- Granular access targets (nullable, depends on GrantType) ---
    public Guid? PackageId { get; set; }
    public Guid? TermId { get; set; }
    public Guid? ContentSectionId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? LessonVideoId { get; set; }
    public Guid? ExamId { get; set; }

    public CodeType GrantType { get; set; } = CodeType.Package;

    public Guid? AccessCodeId { get; set; }
    public AccessCode? AccessCode { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
