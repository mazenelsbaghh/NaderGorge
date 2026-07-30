using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;
namespace NaderGorge.Domain.Entities;
public sealed class ApprovalStepInstance : BaseEntity
{
    public Guid ApprovalInstanceId { get; set; } public ApprovalInstance? ApprovalInstance { get; set; }
    public Guid ApprovalDefinitionStepId { get; set; } public ApprovalDefinitionStep? DefinitionStep { get; set; }
    public int Order { get; set; } public ApprovalStepState State { get; set; } = ApprovalStepState.Pending;
    public Guid? OriginalApproverUserId { get; set; } public Guid? ActingUserId { get; set; } public Guid? DelegationId { get; set; }
    public DateTime DueAt { get; set; } public DateTime? DecidedAt { get; set; } public string? DecisionReason { get; set; }
    public int EscalationLevel { get; set; } public int Version { get; set; } = 1;
}
