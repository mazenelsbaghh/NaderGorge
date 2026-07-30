using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

/// <summary>Effective-dated, audited terms used to calculate future teacher allocations.</summary>
public class TeacherFinancialAgreement : BaseEntity
{
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;
    public TeacherAgreementScopeType ScopeType { get; set; }
    public Guid? ScopeId { get; set; }
    public TeacherAgreementTrigger Trigger { get; set; }
    public TeacherAgreementAllocationMode AllocationMode { get; set; }
    public decimal AllocationValue { get; set; }
    public TeacherPriceBasis PriceBasis { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string Reason { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public class CodeGroupFinancialTerms : BaseEntity
{
    public Guid CodeGroupId { get; set; }
    public CodeGroup CodeGroup { get; set; } = null!;
    public TeacherAgreementTrigger Trigger { get; set; } = TeacherAgreementTrigger.CodeActivation;
    public Guid? AgreementId { get; set; }
    public TeacherFinancialAgreement? Agreement { get; set; }
    public string? Recipient { get; set; }
    public Guid UpdatedByUserId { get; set; }
}

public class CodeGroupDeliveryConfirmation : BaseEntity
{
    public Guid CodeGroupId { get; set; }
    public CodeGroup CodeGroup { get; set; } = null!;
    public string Recipient { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public Guid ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAt { get; set; } = DateTime.UtcNow;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class TeacherSettlement : BaseEntity
{
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string Currency { get; set; } = "EGP";
    public TeacherSettlementStatus Status { get; set; } = TeacherSettlementStatus.Draft;
    public decimal GrossDueAmount { get; set; }
    public decimal DebtDeductionAmount { get; set; }
    public decimal NetPayableAmount { get; set; }
    public string? Note { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PaidByUserId { get; set; }
    public DateTime? PaidAt { get; set; }
    public ICollection<TeacherSettlementLine> Lines { get; set; } = new List<TeacherSettlementLine>();
    public ICollection<TeacherSettlementPayment> Payments { get; set; } = new List<TeacherSettlementPayment>();
}

public class TeacherSettlementLine : BaseEntity
{
    public Guid TeacherSettlementId { get; set; }
    public TeacherSettlement TeacherSettlement { get; set; } = null!;
    public Guid? AllocationId { get; set; }
    public TeacherFinancialAllocation? Allocation { get; set; }
    public Guid? AdjustmentId { get; set; }
    public TeacherPayoutAdjustment? Adjustment { get; set; }
    public decimal Amount { get; set; }
    public string DescriptionSnapshot { get; set; } = string.Empty;
}

public class TeacherSettlementPayment : BaseEntity
{
    public Guid TeacherSettlementId { get; set; }
    public TeacherSettlement TeacherSettlement { get; set; } = null!;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransferReference { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public Guid PaidByUserId { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}

public class FinancialInvoice : BaseEntity
{
    public FinancialInvoiceType Type { get; set; }
    public FinancialInvoiceStatus Status { get; set; } = FinancialInvoiceStatus.Draft;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
    public decimal Amount { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? TeacherSettlementId { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? PaymentReference { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
}
