using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class WorkCalendar : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Africa/Cairo";
    public int WorkingDaysMask { get; set; } = 62;
    public string HolidaysJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public ICollection<ShiftTemplate> ShiftTemplates { get; set; } = new List<ShiftTemplate>();
}
