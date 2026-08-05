using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class FinancialAccount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FinancialAccountType Type { get; set; }
    public FinancialNormalSide NormalSide { get; set; }
    public FinancialAccountRole Role { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class JournalEntry : BaseEntity
{
    public long SequenceNumber { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string PostingKind { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string? CorrelationId { get; set; }
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Posted;
    public Guid? ReversalOfId { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}

public sealed class JournalLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? TreasuryAccountId { get; set; }
    public string? DimensionKey { get; set; }
    public string? Memo { get; set; }
}

public sealed class TreasuryAccount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public TreasuryAccountType Type { get; set; }
    public Guid FinancialAccountId { get; set; }
    public Guid? DigitalWalletId { get; set; }
    public string? MaskedIdentifier { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AccountingPeriod : BaseEntity
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;
    public Guid? ClosedByUserId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
}
