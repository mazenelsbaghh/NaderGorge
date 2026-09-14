using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;
namespace NaderGorge.Domain.Entities;
public sealed class ApprovalInstance : BaseEntity
{
    public Guid ApprovalDefinitionId { get; set; } public ApprovalDefinition? ApprovalDefinition { get; set; }
    public string RequestType { get; set; } = string.Empty; public Guid RequestId { get; set; }
    public Guid RequesterEmployeeId { get; set; } public EmployeeProfile? RequesterEmployee { get; set; }
    public ApprovalInstanceState State { get; set; } = ApprovalInstanceState.Pending; public int CurrentStepOrder { get; set; } = 1; public int Version { get; set; } = 1;
    public ICollection<ApprovalStepInstance> Steps { get; set; } = new List<ApprovalStepInstance>();
}
