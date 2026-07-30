using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class HrModuleRollout : BaseEntity
{
    public string Module { get; set; } = string.Empty;
    public HrModuleRolloutState State { get; set; } = HrModuleRolloutState.Legacy;
    public string ReadTarget { get; set; } = "legacy";
    public string WriteTarget { get; set; } = "legacy";
    public Guid? ChangedByUserId { get; set; }
    public DateTime? ChangedAt { get; set; }
    public Guid? ReconciliationBatchId { get; set; }
    public string? Reason { get; set; }
}
