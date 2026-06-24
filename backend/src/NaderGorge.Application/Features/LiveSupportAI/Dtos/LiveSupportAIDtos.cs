namespace NaderGorge.Application.Features.LiveSupportAI.Dtos;

public sealed record LiveSupportAICatalogItemDto(string Key, string Label, string Description, bool RequiresVerification = false);
public sealed record LiveSupportAICatalogsDto(
    IReadOnlyList<LiveSupportAICatalogItemDto> ReadableData,
    IReadOnlyList<LiveSupportAICatalogItemDto> Actions,
    IReadOnlyList<LiveSupportAICatalogItemDto> LookupKeys,
    IReadOnlyList<LiveSupportAICatalogItemDto> VerificationQuestions);

public sealed record LiveSupportAIPolicyDto(
    Guid Id,
    long VersionNumber,
    string Status,
    bool IsEnabled,
    string SystemInstructions,
    IReadOnlyList<string> ReadableDataKeys,
    IReadOnlyList<string> ActionKeys,
    IReadOnlyList<string> LookupKeys,
    IReadOnlyList<string> VerificationQuestionKeys,
    int VerificationRequiredCorrect,
    int VerificationMaxAttempts,
    int PendingActionExpirySeconds,
    int InactivityMinutes,
    int InactivityWarningGraceSeconds,
    long Version,
    DateTime? PublishedAt);

public sealed record LiveSupportAIConfigDto(LiveSupportAIPolicyDto? Draft, LiveSupportAIPolicyDto? Published, LiveSupportAICatalogsDto Catalogs);

public sealed record SaveLiveSupportAIDraftRequest(
    string SystemInstructions,
    IReadOnlyList<string> ReadableDataKeys,
    IReadOnlyList<string> ActionKeys,
    IReadOnlyList<string> LookupKeys,
    IReadOnlyList<string> VerificationQuestionKeys,
    int VerificationRequiredCorrect,
    int VerificationMaxAttempts,
    int PendingActionExpirySeconds,
    int InactivityMinutes,
    int InactivityWarningGraceSeconds,
    long? ExpectedVersion);

public sealed record LiveSupportAIStatsDto(
    int ActiveConversations,
    int ResolvedIssues,
    int Handoffs,
    int TotalMessagesSent,
    int SuccessfulActions);

public sealed record LiveSupportAIKnowledgeRevisionDto(
    Guid EntryId, Guid RevisionId, string Title, int RevisionNumber, string Content, string? SourceLabel,
    bool IsPublished, DateTime? ValidFrom, DateTime? ValidUntil, DateTime? PublishedAt);
public sealed record SaveLiveSupportAIKnowledgeRequest(
    Guid? EntryId, string Title, string Content, string? SourceLabel, bool Publish, DateTime? ValidFrom, DateTime? ValidUntil);
public sealed record LinkLiveSupportAIKnowledgeRequest(Guid PolicyVersionId, IReadOnlyList<Guid> RevisionIds);
public sealed record LiveSupportAIWorkerPreviewResultDto(
    LiveSupportAIWorkerDecisionDto Decision,
    string DecisionHash,
    string Provider,
    string Model,
    int LatencyMs);

public sealed record LiveSupportAIPreviewResultDto(
    Guid PolicyVersionId,
    bool DryRun,
    int KnowledgeDocuments,
    IReadOnlyList<string> AllowedDecisionTypes,
    string SafeOutcome,
    LiveSupportAIWorkerDecisionDto Decision,
    string DecisionHash,
    string Provider,
    string Model,
    int LatencyMs);
public sealed record LiveSupportAIEvidenceItemDto(Guid TurnId, Guid ConversationId, DateTime At, string Status, string? DecisionType, string? FailureCode, string? Provider, string? Model, int CallbackAttempts);
public sealed record LiveSupportAIEvidencePageDto(IReadOnlyList<LiveSupportAIEvidenceItemDto> Items, string? NextCursor);
