using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class EmployeeVacation : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public VacationStatus Status { get; set; } = VacationStatus.Pending;
    public string Reason { get; set; } = string.Empty;

    public Guid? HandledBy { get; set; }
    public User? HandledByUser { get; set; }
    public DateTime? HandledAt { get; set; }
}
