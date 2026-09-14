using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class ShiftSegment : BaseEntity
{
    public Guid ShiftTemplateId { get; set; }
    public ShiftTemplate? ShiftTemplate { get; set; }
    public int Sequence { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeSpan StartsAt { get; set; }
    public TimeSpan EndsAt { get; set; }
    public int UnpaidBreakMinutes { get; set; }
    public ShiftWorkDateRule WorkDateRule { get; set; } = ShiftWorkDateRule.SegmentStartDate;
}
