using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAIPolicyVersion : BaseEntity
{
    public long VersionNumber { get; set; }
    public LiveSupportAIPolicyStatus Status { get; set; }
    public bool IsEnabled { get; set; }
    public string SystemInstructions { get; set; } = string.Empty;
    public string ReadableDataKeysJson { get; set; } = "[]";
    public string ActionKeysJson { get; set; } = "[]";
    public string LookupKeysJson { get; set; } = "[]";
    public string VerificationQuestionKeysJson { get; set; } = "[]";
    public int VerificationRequiredCorrect { get; set; } = 1;
    public int VerificationMaxAttempts { get; set; } = 3;
    public int PendingActionExpirySeconds { get; set; } = 300;
    public int InactivityMinutes { get; set; } = 30;
    public int InactivityWarningGraceSeconds { get; set; } = 120;
    public Guid CreatedByUserId { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long Version { get; set; }
}
