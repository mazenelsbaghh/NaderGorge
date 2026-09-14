using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class TeacherStaffMember : BaseEntity
{
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid CreatedByTeacherUserId { get; set; }
    public User CreatedByTeacherUser { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string PermissionKeys { get; set; } = string.Empty;
}
