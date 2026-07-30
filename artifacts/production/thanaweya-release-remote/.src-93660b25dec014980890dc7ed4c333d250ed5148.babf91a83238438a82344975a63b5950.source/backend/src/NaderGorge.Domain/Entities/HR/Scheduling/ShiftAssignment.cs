using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class ShiftAssignment : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public ShiftTemplate? ShiftTemplate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public ShiftAssignmentStatus Status { get; set; } = ShiftAssignmentStatus.Draft;
    public Guid? ReplacesAssignmentId { get; set; }
    public ShiftAssignment? ReplacesAssignment { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid PublishedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
}
