using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class ShiftSwapRequest : BaseEntity
{
    public Guid RequesterEmployeeId { get; set; }
    public EmployeeProfile? RequesterEmployee { get; set; }
    public Guid RequesterAssignmentId { get; set; }
    public ShiftAssignment? RequesterAssignment { get; set; }
    public Guid TargetEmployeeId { get; set; }
    public EmployeeProfile? TargetEmployee { get; set; }
    public Guid TargetAssignmentId { get; set; }
    public ShiftAssignment? TargetAssignment { get; set; }
    public ShiftSwapStatus Status { get; set; } = ShiftSwapStatus.PendingManager;
    public string Reason { get; set; } = string.Empty;
    public Guid? ManagerDecisionByUserId { get; set; }
    public Guid? HrDecisionByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public int Version { get; set; } = 1;
}
