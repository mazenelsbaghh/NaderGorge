using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class AttendancePolicyException : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public bool AllowRemote { get; set; }
    public Guid? OverridePolicyId { get; set; }
    public AttendancePolicy? OverridePolicy { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ApprovedByUserId { get; set; }
}
