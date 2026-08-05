using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public enum FinanceBudgetPeriodKind
{
    Week = 1,
    Month = 2,
    Year = 3,
    Custom = 4
}

public enum FinanceBudgetStatus
{
    Draft = 1,
    Active = 2,
    Archived = 3
}

public sealed class FinanceBudgetPlan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public FinanceBudgetPeriodKind PeriodKind { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Version { get; set; } = 1;
    public FinanceBudgetStatus Status { get; set; } = FinanceBudgetStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public ICollection<FinanceBudgetLine> Lines { get; set; } = new List<FinanceBudgetLine>();
}

public sealed class FinanceBudgetLine : BaseEntity
{
    public Guid FinanceBudgetPlanId { get; set; }
    public Guid FinancialAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? TeacherId { get; set; }
    public decimal PlannedAmount { get; set; }
}

public sealed class TreasuryTransfer : BaseEntity
{
    public Guid SourceTreasuryAccountId { get; set; }
    public Guid DestinationTreasuryAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Reference { get; set; } = string.Empty;
    public Guid JournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
}

public sealed class TreasuryReconciliation : BaseEntity
{
    public Guid TreasuryAccountId { get; set; }
    public DateTime AsOfDate { get; set; }
    public decimal SystemBalance { get; set; }
    public decimal CountedOrStatementBalance { get; set; }
    public decimal Variance => CountedOrStatementBalance - SystemBalance;
    public string EvidenceNote { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public Guid? AdjustmentJournalEntryId { get; set; }
}
