using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class StudentNote : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid AdminId { get; set; }
    public User Admin { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    /// <summary>Pinned notes appear first</summary>
    public bool IsPinned { get; set; }
}
