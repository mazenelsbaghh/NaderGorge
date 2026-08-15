using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class GiftIssuance : BaseEntity
{
    public Guid RequestId { get; set; }
    public GiftTargetType TargetType { get; set; }
    public Guid? PackageId { get; set; }
    public Package? Package { get; set; }
    public Guid? TermId { get; set; }
    public Term? Term { get; set; }
    public Guid? ContentSectionId { get; set; }
    public ContentSection? ContentSection { get; set; }
    public Guid? LessonId { get; set; }
    public Lesson? Lesson { get; set; }
    public Guid? LessonVideoId { get; set; }
    public LessonVideo? LessonVideo { get; set; }
    public Guid? ExamId { get; set; }
    public Exam? Exam { get; set; }
    public Guid? TeacherId { get; set; }
    public TeacherProfile? Teacher { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid IssuedByUserId { get; set; }
    public User IssuedByUser { get; set; } = null!;
    public GiftIssuanceStatus Status { get; set; } = GiftIssuanceStatus.Active;
    public ICollection<GiftRecipient> Recipients { get; set; } = new List<GiftRecipient>();

    public Guid? GetTargetId() =>
        PackageId ?? TermId ?? ContentSectionId ?? LessonId ?? LessonVideoId ?? ExamId;
}

public class GiftRecipient : BaseEntity
{
    public Guid GiftIssuanceId { get; set; }
    public GiftIssuance GiftIssuance { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public GiftRecipientStatus Status { get; set; } = GiftRecipientStatus.Granted;
    public string OutcomeCode { get; set; } = string.Empty;
    public string? OutcomeMessage { get; set; }
    public int UsesConsumed { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }
    public string? RevocationReason { get; set; }
    public StudentAccessGrant? AccessGrant { get; set; }
    public PromotionalBalanceAllocation? PromotionalBalanceAllocation { get; set; }
}

public class PromotionalBalanceAllocation : BaseEntity
{
    public Guid GiftRecipientId { get; set; }
    public GiftRecipient GiftRecipient { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public Guid? TeacherId { get; set; }
    public TeacherProfile? Teacher { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal ConsumedAmount { get; set; }
    public decimal ExpiredAmount { get; set; }
    public decimal RevokedAmount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? MaxPurchaseCount { get; set; }
    public int PurchaseCount { get; set; }
    public PromotionalBalanceStatus Status { get; set; } = PromotionalBalanceStatus.Active;
    public ICollection<PromotionalBalanceUsage> Usages { get; set; } = new List<PromotionalBalanceUsage>();
}

public class PromotionalBalanceUsage : BaseEntity
{
    public Guid AllocationId { get; set; }
    public PromotionalBalanceAllocation Allocation { get; set; } = null!;
    public Guid GiftRecipientId { get; set; }
    public GiftRecipient GiftRecipient { get; set; } = null!;
    public Guid PurchaseOperationId { get; set; }
    public CodeType ContentType { get; set; }
    public Guid ContentId { get; set; }
    public decimal Amount { get; set; }
}
