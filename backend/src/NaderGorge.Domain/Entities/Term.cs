using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class Term : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }

    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    // Navigation
    public ICollection<ContentSection> Sections { get; set; } = new List<ContentSection>();
}
