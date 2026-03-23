using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? ParentPhone { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? School { get; set; }
    public string? Grade { get; set; }
    public string? Track { get; set; }
}
