using NaderGorge.Domain.Common;
namespace NaderGorge.Domain.Entities;
public sealed class ApprovalDefinition : BaseEntity
{
    public string RequestType { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true; public int Version { get; set; } = 1;
    public ICollection<ApprovalDefinitionStep> Steps { get; set; } = new List<ApprovalDefinitionStep>();
}
