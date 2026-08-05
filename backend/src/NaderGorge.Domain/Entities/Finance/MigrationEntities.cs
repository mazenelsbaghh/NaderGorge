using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public enum FinanceMigrationBatchStatus
{
    Previewed = 1,
    Running = 2,
    Completed = 3,
    CompletedWithErrors = 4
}

public enum FinanceMigrationItemStatus
{
    Candidate = 1,
    Posted = 2,
    AlreadyPosted = 3,
    Ignored = 4,
    Failed = 5
}

public sealed class FinancialMigrationBatch : BaseEntity
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public FinanceMigrationBatchStatus Status { get; set; } = FinanceMigrationBatchStatus.Running;
    public int CandidateCount { get; set; }
    public int PostedCount { get; set; }
    public int AlreadyPostedCount { get; set; }
    public int FailedCount { get; set; }
    public string SourceChecksum { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<FinancialMigrationItem> Items { get; set; } = new List<FinancialMigrationItem>();
    public ICollection<FinancialMigrationException> Exceptions { get; set; } = new List<FinancialMigrationException>();
}

public sealed class FinancialMigrationItem : BaseEntity
{
    public Guid FinancialMigrationBatchId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string SourceChecksum { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public FinanceMigrationItemStatus Status { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class FinancialMigrationException : BaseEntity
{
    public Guid FinancialMigrationBatchId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
