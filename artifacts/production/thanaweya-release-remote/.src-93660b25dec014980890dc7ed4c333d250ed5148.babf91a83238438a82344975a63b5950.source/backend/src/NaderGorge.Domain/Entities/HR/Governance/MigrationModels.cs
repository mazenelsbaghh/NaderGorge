using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class HrMigrationBatch : BaseEntity
{
    public string Module { get; set; } = string.Empty; public string SourceSystem { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty; public HrMigrationBatchState State { get; set; } = HrMigrationBatchState.DryRun;
    public int SourceCount { get; set; } public int TargetCount { get; set; } public decimal SourceTotal { get; set; } public decimal TargetTotal { get; set; }
    public string SourceHash { get; set; } = string.Empty; public string? TargetHash { get; set; } public Guid CreatedByUserId { get; set; }
    public DateTime? ReconciledAt { get; set; } public string ReportJson { get; set; } = "{}";
    public ICollection<HrMigrationRecordMap> RecordMaps { get; set; } = new List<HrMigrationRecordMap>(); public ICollection<HrMigrationConflict> Conflicts { get; set; } = new List<HrMigrationConflict>();
}
public sealed class HrMigrationRecordMap : BaseEntity
{
    public Guid MigrationBatchId { get; set; } public HrMigrationBatch? MigrationBatch { get; set; }
    public string SourceType { get; set; } = string.Empty; public string SourceId { get; set; } = string.Empty; public string SourceHash { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty; public Guid TargetId { get; set; } public decimal Amount { get; set; }
}
public sealed class HrMigrationConflict : BaseEntity
{
    public Guid MigrationBatchId { get; set; } public HrMigrationBatch? MigrationBatch { get; set; }
    public string SourceType { get; set; } = string.Empty; public string SourceId { get; set; } = string.Empty; public string Code { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}"; public HrMigrationConflictState State { get; set; } = HrMigrationConflictState.Open;
    public Guid? ResolvedByUserId { get; set; } public string? ResolutionReason { get; set; }
}
