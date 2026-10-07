using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class LessonMimStudio : BaseEntity
{
    public Guid LessonId { get; set; }
    public Guid? SourceVideoId { get; set; }
    public int SourceRevision { get; set; }
    public DateTime? GenerationStartedAt { get; set; }
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

public sealed class MimSceneVideo : BaseEntity
{
    public Guid LessonId { get; set; }
    public int SceneIndex { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid ScriptVersion { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public string State { get; set; } = "quoted";
    public string ParametersJson { get; set; } = "{}";
    public string QuoteText { get; set; } = "";
    public Guid? JobId { get; set; }
    public string ResultJson { get; set; } = "{}";
    public DateTime QuoteExpiresAt { get; set; }
}
