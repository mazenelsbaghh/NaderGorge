using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class AttendancePolicyAssignment : BaseEntity
{
    public Guid AttendancePolicyId { get; set; }
    public AttendancePolicy? AttendancePolicy { get; set; }
    public Guid? EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public Guid? ShiftTemplateId { get; set; }
    public ShiftTemplate? ShiftTemplate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
