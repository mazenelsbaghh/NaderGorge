using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class TeacherPhoto : BaseEntity
{
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
