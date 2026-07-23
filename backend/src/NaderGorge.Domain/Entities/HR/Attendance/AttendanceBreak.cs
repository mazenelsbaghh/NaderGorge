using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class AttendanceBreak : BaseEntity
{
    public Guid AttendanceSessionId { get; set; }
    public AttendanceSession? AttendanceSession { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int Version { get; set; } = 1;
}
