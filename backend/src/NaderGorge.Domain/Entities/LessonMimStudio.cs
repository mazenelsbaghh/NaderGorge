using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class LessonMimStudio : BaseEntity
{
    public Guid LessonId { get; set; }
    public Guid SourceVideoId { get; set; }
    public int SourceRevision { get; set; }
    public string DocumentJson { get; set; } = "{}";
    public Guid Version { get; set; } = Guid.NewGuid();
    public Guid UpdatedByUserId { get; set; }
}

public sealed class HiggsfieldMcpConnection : BaseEntity
{
    public Guid AdminUserId { get; set; }
    public string ClientId { get; set; } = "";
    public string ProtectedSession { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
}
