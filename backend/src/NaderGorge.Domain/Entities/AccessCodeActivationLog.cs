using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class AccessCodeActivationLog : BaseEntity
{
    public Guid AccessCodeId { get; set; }
    public AccessCode AccessCode { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid? PackageId { get; set; }
    public Package? Package { get; set; }

    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionEarned { get; set; }
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
}
