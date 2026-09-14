using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class AttendanceSession : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public Guid ShiftAssignmentId { get; set; }
    public ShiftAssignment? ShiftAssignment { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime ClockedInAt { get; set; }
    public DateTime? ClockedOutAt { get; set; }
    public AttendanceSessionState State { get; set; } = AttendanceSessionState.Open;
    public int Version { get; set; } = 1;
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public int WorkedMinutes { get; set; }
    public ICollection<AttendanceBreak> Breaks { get; set; } = new List<AttendanceBreak>();
}
