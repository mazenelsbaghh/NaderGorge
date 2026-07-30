using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;
namespace NaderGorge.Domain.Entities;
public sealed class HrLeaveRequest : BaseEntity
{
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public Guid LeaveTypeId { get; set; } public LeaveType? LeaveType { get; set; }
    public DateOnly StartDate { get; set; } public DateOnly EndDate { get; set; } public decimal DayFraction { get; set; } = 1;
    public decimal Workdays { get; set; } public decimal ReservedAmount { get; set; }
    public string Reason { get; set; } = string.Empty; public string? AttachmentReference { get; set; }
    public LeaveRequestState State { get; set; } = LeaveRequestState.Draft;
    public Guid? ApprovalInstanceId { get; set; } public ApprovalInstance? ApprovalInstance { get; set; }
    public int Version { get; set; } = 1;
}
