using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class OrganizationUnit : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public OrganizationUnitType Type { get; set; }
    public Guid? ParentId { get; set; }
    public OrganizationUnit? Parent { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public EmployeeProfile? ManagerEmployee { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
