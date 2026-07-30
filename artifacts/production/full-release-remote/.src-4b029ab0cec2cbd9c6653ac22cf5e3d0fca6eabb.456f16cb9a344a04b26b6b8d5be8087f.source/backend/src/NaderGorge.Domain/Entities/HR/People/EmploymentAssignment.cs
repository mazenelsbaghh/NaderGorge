using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class EmploymentAssignment : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }
    public Guid? JobPositionId { get; set; }
    public JobPosition? JobPosition { get; set; }
    public Guid? JobGradeId { get; set; }
    public JobGrade? JobGrade { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public EmployeeProfile? ManagerEmployee { get; set; }
    public Guid? WorkLocationId { get; set; }
    public WorkLocation? WorkLocation { get; set; }
    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
}
