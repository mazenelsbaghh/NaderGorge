using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public enum PlatformExpenseStatus
{
    Draft = 1,
    PostedUnpaid = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Reversed = 5
}

public enum PlatformRefundMethod
{
    StudentBalance = 1,
    Cash = 2
}

public enum PlatformRefundStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

public sealed class ExpenseCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string AccountCode { get; set; } = "5000";
    public bool IsActive { get; set; } = true;
}

public sealed class FinanceCostCenter : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class FinanceVendor : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PlatformExpense : BaseEntity
{
    public string DocumentNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public PlatformExpenseStatus Status { get; set; } = PlatformExpenseStatus.Draft;
    public Guid CategoryId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? TreasuryAccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public byte[] Version { get; set; } = Array.Empty<byte>();
    public ICollection<ExpensePayment> Payments { get; set; } = new List<ExpensePayment>();
}

public sealed class ExpensePayment : BaseEntity
{
    public Guid PlatformExpenseId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public Guid TreasuryAccountId { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public Guid JournalEntryId { get; set; }
    public Guid PaidByUserId { get; set; }
}

public sealed class PlatformRefund : BaseEntity
{
    public Guid OriginalSourceId { get; set; }
    public string OriginalSourceType { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Guid? TeacherId { get; set; }
    public decimal PlatformAmount { get; set; }
    public decimal TeacherAmount { get; set; }
    public decimal TotalAmount => PlatformAmount + TeacherAmount;
    public PlatformRefundMethod Method { get; set; }
    public PlatformRefundStatus Status { get; set; } = PlatformRefundStatus.Draft;
    public Guid? TreasuryAccountId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
}
