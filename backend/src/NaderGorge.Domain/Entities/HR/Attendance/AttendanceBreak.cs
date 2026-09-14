using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class AttendanceBreak : BaseEntity
{
    public Guid AttendanceSessionId { get; set; }
    public AttendanceSession? AttendanceSession { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public AttendanceBreakKind Kind { get; set; } = AttendanceBreakKind.Regular;
    public int AllowedMinutes { get; set; }
    public int Version { get; set; } = 1;
}
