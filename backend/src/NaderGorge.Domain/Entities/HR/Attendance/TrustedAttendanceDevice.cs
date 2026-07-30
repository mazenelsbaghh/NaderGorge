using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class TrustedAttendanceDevice : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public Guid ApprovedByUserId { get; set; }
}
