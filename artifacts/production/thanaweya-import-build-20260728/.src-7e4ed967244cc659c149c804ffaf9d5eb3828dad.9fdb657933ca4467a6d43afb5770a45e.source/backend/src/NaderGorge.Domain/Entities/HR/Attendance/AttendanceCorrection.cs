using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class AttendanceCorrection : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public Guid AttendanceSessionId { get; set; }
    public AttendanceSession? AttendanceSession { get; set; }
    public DateTime? ProposedClockedInAt { get; set; }
    public DateTime? ProposedClockedOutAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceReference { get; set; }
    public AttendanceCorrectionState State { get; set; } = AttendanceCorrectionState.PendingManager;
    public string BeforeJson { get; set; } = "{}";
    public string? AppliedJson { get; set; }
    public Guid? ManagerDecisionByUserId { get; set; }
    public Guid? HrDecisionByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? AppliedAt { get; set; }
    public int Version { get; set; } = 1;
}
