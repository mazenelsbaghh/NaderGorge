using NaderGorge.Domain.Common;
namespace NaderGorge.Domain.Entities;
public sealed class LeavePolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty; public Guid LeaveTypeId { get; set; } public LeaveType? LeaveType { get; set; }
    public decimal AnnualEntitlement { get; set; } public decimal MaximumCarryover { get; set; }
    public bool AllowNegativeBalance { get; set; } public DateOnly EffectiveFrom { get; set; } public DateOnly? EffectiveTo { get; set; }
    public Guid WorkCalendarId { get; set; } public WorkCalendar? WorkCalendar { get; set; }
}
