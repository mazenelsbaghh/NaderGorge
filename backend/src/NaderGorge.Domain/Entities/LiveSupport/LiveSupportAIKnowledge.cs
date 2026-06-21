using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAIKnowledgeEntry : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportAIKnowledgeRevision : BaseEntity
{
    public Guid EntryId { get; set; }
    public int RevisionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? SourceLabel { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class LiveSupportAIPolicyKnowledgeRevision
{
    public Guid PolicyVersionId { get; set; }
    public Guid KnowledgeRevisionId { get; set; }
}
