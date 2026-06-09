using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class AttendanceLog : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }

    public DateOnly Date { get; set; }
    public DateTime ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public int LateMinutes { get; set; }
    public AttendanceStatus Status { get; set; }

    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}
