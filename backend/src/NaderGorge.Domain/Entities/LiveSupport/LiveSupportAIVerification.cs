using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAIVerificationPolicyQuestion : BaseEntity
{
    public Guid PolicyVersionId { get; set; }
    public string QuestionKey { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public string SourceFieldKey { get; set; } = string.Empty;
    public LiveSupportAIComparisonMode ComparisonMode { get; set; }
    public int Order { get; set; }
}

public sealed class LiveSupportAIVerificationSession : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid PolicyVersionId { get; set; }
    public Guid? CandidateStudentUserId { get; set; }
    public string LookupKey { get; set; } = string.Empty;
    public string LookupValueHash { get; set; } = string.Empty;
    public string SelectedQuestionKeysJson { get; set; } = "[]";
    public int RequiredCorrect { get; set; }
    public int CorrectCount { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public LiveSupportAIVerificationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportAIVerificationAttempt : BaseEntity
{
    public Guid SessionId { get; set; }
    public string QuestionKeysJson { get; set; } = "[]";
    public string OutcomeCodesJson { get; set; } = "[]";
    public DateTime SubmittedAt { get; set; }
    public int AttemptNumber { get; set; }
}
