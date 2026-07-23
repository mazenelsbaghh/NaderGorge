using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class TeacherAccount : BaseEntity
{
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public decimal TotalEarnings { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal ReservedBalance { get; set; }
    public decimal CommissionRate { get; set; }
    public long Version { get; set; }
}
