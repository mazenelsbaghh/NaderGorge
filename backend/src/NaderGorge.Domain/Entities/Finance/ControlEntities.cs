using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

/// <summary>Durable watermark used by shadow posting and reconciliation jobs.</summary>
public sealed class FinancialProjectionCheckpoint : BaseEntity
{
    public string SourceType { get; set; } = string.Empty;
    public DateTime? LastOccurredAt { get; set; }
    public Guid? LastSourceId { get; set; }
    public long SourceCount { get; set; }
    public decimal SourceAmount { get; set; }
    public decimal PostedAmount { get; set; }
    public decimal Variance { get; set; }
    public DateTime LastReconciledAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
