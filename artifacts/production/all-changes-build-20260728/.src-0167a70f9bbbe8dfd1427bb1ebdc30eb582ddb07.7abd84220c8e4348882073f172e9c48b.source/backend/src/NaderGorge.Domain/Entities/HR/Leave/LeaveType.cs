using NaderGorge.Domain.Common;
namespace NaderGorge.Domain.Entities;
public sealed class LeaveType : BaseEntity
{
    public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true; public bool RequiresAttachment { get; set; }
    public bool AllowsHalfDay { get; set; } = true; public bool IsActive { get; set; } = true;
}
