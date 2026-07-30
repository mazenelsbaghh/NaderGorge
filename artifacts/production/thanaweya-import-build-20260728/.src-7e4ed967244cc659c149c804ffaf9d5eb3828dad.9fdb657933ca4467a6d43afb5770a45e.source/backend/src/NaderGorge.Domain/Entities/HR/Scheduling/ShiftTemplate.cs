using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class ShiftTemplate : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ShiftTemplateMode Mode { get; set; }
    public Guid WorkCalendarId { get; set; }
    public WorkCalendar? WorkCalendar { get; set; }
    public int GraceMinutes { get; set; }
    public int MinimumBreakMinutes { get; set; }
    public int OvertimeAfterMinutes { get; set; } = 480;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public ICollection<ShiftSegment> Segments { get; set; } = new List<ShiftSegment>();
}
