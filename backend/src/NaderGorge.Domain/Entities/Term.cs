using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class Term : BaseEntity, IArchivableContent
{
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Order { get; set; }
    public decimal Price { get; set; }
    public bool IsSystemContainer { get; set; }
    public ContentArchiveMode ArchiveMode { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    // Navigation
    public ICollection<ContentSection> Sections { get; set; } = new List<ContentSection>();
}
