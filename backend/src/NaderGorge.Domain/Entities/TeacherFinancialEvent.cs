using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class TeacherFinancialEvent : BaseEntity
{
    public TeacherFinancialSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid? StudentId { get; set; }
    public User? Student { get; set; }
    public SalesTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PlatformDiscountAmount { get; set; }
    public decimal TeacherDiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PromotionalAmount { get; set; }
    public decimal PlatformShareAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public TeacherFinancialReviewStatus ReviewStatus { get; set; } = TeacherFinancialReviewStatus.AutoApproved;
    public TeacherFinancialPayoutStatus PayoutStatus { get; set; } = TeacherFinancialPayoutStatus.Unpaid;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public ICollection<TeacherFinancialAllocation> Allocations { get; set; } = new List<TeacherFinancialAllocation>();
}

public class TeacherFinancialAllocation : BaseEntity
{
    public Guid TeacherFinancialEventId { get; set; }
    public TeacherFinancialEvent TeacherFinancialEvent { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;
    public TeacherAllocationMode AllocationMode { get; set; }
    public decimal AllocationValue { get; set; }
    public decimal GrossBasisAmount { get; set; }
    public decimal TeacherShareAmount { get; set; }
    public decimal PlatformShareAmount { get; set; }
    public Guid? AgreementId { get; set; }
    public TeacherAgreementScopeType? AgreementScopeType { get; set; }
    public Guid? AgreementScopeId { get; set; }
    public TeacherAgreementAllocationMode? AgreementAllocationMode { get; set; }
    public TeacherPriceBasis? PriceBasis { get; set; }
    public TeacherDiscountBearer DiscountBearer { get; set; } = TeacherDiscountBearer.Platform;
    public decimal ReversedAmount { get; set; }
    public Guid? SettlementLineId { get; set; }
    public string? StudentNameSnapshot { get; set; }
    public string? StudentPhoneSnapshot { get; set; }
    public string ContentNameSnapshot { get; set; } = string.Empty;
    public long? CodeSerialNumber { get; set; }
    public TeacherFinancialReviewStatus ReviewStatus { get; set; } = TeacherFinancialReviewStatus.AutoApproved;
    public TeacherFinancialPayoutStatus PayoutStatus { get; set; } = TeacherFinancialPayoutStatus.Unpaid;
    public Guid? PayoutId { get; set; }
    public TeacherPayout? Payout { get; set; }
}

public class TeacherPayoutAdjustment : BaseEntity
{
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;
    public Guid? RelatedFinancialEventId { get; set; }
    public TeacherFinancialEvent? RelatedFinancialEvent { get; set; }
    public Guid? RelatedPayoutId { get; set; }
    public TeacherPayout? RelatedPayout { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public TeacherPayoutAdjustmentStatus Status { get; set; } = TeacherPayoutAdjustmentStatus.Open;
}
