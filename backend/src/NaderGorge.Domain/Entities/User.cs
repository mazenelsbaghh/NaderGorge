using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public bool IsProfileComplete { get; set; } = false;
    public string? SuspensionReason { get; set; }
    public int PasswordResetVersion { get; set; } = 0;
    public int SecurityStampVersion { get; set; } = 0;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public StudentProfile? StudentProfile { get; set; }
    public StudentBalance? StudentBalance { get; set; }
    public EmployeeProfile? EmployeeProfile { get; set; }
    public TeacherProfile? TeacherProfile { get; set; }
    public ICollection<TeacherStaffMember> TeacherStaffMemberships { get; set; } = new List<TeacherStaffMember>();
    public ICollection<TeacherStaffMember> CreatedTeacherStaffMembers { get; set; } = new List<TeacherStaffMember>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
