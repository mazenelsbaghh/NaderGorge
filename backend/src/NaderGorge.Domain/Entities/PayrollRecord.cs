using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class PayrollRecord : BaseEntity
{
    public Guid EmployeeProfileId { get; set; }
    public EmployeeProfile EmployeeProfile { get; set; } = null!;

    public int Month { get; set; }
    public int Year { get; set; }

    public decimal BasicSalary { get; set; }
    
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public DateTime? ApprovedAt { get; set; }

    // Navigation property
    public ICollection<PayrollAdjustment> Adjustments { get; set; } = new List<PayrollAdjustment>();
}
