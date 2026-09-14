using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class AttendanceAttempt : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public AttendanceEventType EventType { get; set; }
    public DateTime OccurredAt { get; set; }
    public bool Accepted { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? AttendancePolicyId { get; set; }
    public AttendancePolicy? AttendancePolicy { get; set; }
    public Guid? AttendanceSessionId { get; set; }
    public AttendanceSession? AttendanceSession { get; set; }
}
