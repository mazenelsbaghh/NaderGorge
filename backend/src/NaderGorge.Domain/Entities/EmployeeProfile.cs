using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class EmployeeProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public decimal BasicSalary { get; set; }
    public TimeSpan StandardStartTime { get; set; } = new TimeSpan(9, 0, 0); // Default to 09:00 AM
    public int TargetDailyHours { get; set; } = 8; // Default to 8 hours
}
