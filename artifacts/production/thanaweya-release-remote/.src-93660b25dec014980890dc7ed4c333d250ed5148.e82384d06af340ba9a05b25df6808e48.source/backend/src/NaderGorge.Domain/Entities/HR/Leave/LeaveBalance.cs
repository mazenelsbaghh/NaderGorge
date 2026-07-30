using NaderGorge.Domain.Common;
namespace NaderGorge.Domain.Entities;
public sealed class LeaveBalance : BaseEntity
{
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public Guid LeaveTypeId { get; set; } public LeaveType? LeaveType { get; set; } public int Year { get; set; }
    public decimal Granted { get; set; } public decimal Carried { get; set; } public decimal Reserved { get; set; } public decimal Used { get; set; }
    public int Version { get; set; } = 1; public decimal Available => Granted + Carried - Reserved - Used;
}
