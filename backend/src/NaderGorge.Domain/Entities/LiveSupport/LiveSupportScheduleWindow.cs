using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportScheduleWindow : BaseEntity
{
    public Guid StaffConfigId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartLocalTime { get; set; }
    public TimeOnly EndLocalTime { get; set; }
    public bool IsActive { get; set; } = true;
}
