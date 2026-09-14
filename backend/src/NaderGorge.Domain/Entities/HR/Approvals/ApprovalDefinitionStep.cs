using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;
namespace NaderGorge.Domain.Entities;
public sealed class ApprovalDefinitionStep : BaseEntity
{
    public Guid ApprovalDefinitionId { get; set; } public ApprovalDefinition? ApprovalDefinition { get; set; }
    public int Order { get; set; } public string Name { get; set; } = string.Empty; public ApprovalApproverKind ApproverKind { get; set; }
    public string? Permission { get; set; } public Guid? SpecificUserId { get; set; }
    public int SlaMinutes { get; set; } = 1440; public string? EscalationPermission { get; set; }
}
