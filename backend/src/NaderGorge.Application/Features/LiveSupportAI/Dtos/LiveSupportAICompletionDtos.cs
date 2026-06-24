using System.Text.Json;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.LiveSupportAI.Dtos;

public static class LiveSupportAIContractLimits
{
    public const int MaxMessageLength = 4_000;
    public const int MaxSafeSummaryLength = 2_000;
    public const int MaxContextCharacters = 40_000;
    public const int MaxKnowledgeDocuments = 8;
    public const int MaxTranscriptMessages = 40;
}

public sealed record LiveSupportAIKnowledgeDocumentDto(Guid RevisionId, string Title, string Content);
public sealed record LiveSupportAIContextMessageDto(string SenderType, string Content, DateTime SentAt);
public sealed record LiveSupportAIAllowedActionDto(string Key, string DescriptionAr, JsonElement ArgumentsSchema);

public sealed record LiveSupportAIWorkerClaimDto(
    string SchemaVersion,
    Guid TurnId,
    Guid ConversationId,
    Guid PolicyVersionId,
    long ExpectedConversationVersion,
    string CallbackIdempotencyKey,
    DateTime DeadlineAt,
    string SystemInstructions,
    IReadOnlyList<LiveSupportAIKnowledgeDocumentDto> KnowledgeDocuments,
    IReadOnlyDictionary<string, object?> StudentContext,
    IReadOnlyList<LiveSupportAIContextMessageDto> Messages,
    IReadOnlyList<LiveSupportAIAllowedActionDto> AllowedActions,
    IReadOnlyList<string> AllowedDecisionTypes);

public sealed record LiveSupportAIWorkerDecisionDto(
    string SchemaVersion,
    string Type,
    string? MessageAr,
    JsonElement? Action,
    JsonElement? Verification,
    JsonElement? AccountCreation,
    JsonElement? Resolution,
    JsonElement? Handoff);

public sealed record LiveSupportAIWorkerCompletionDto(
    string SchemaVersion,
    long ExpectedConversationVersion,
    Guid ExpectedPolicyVersionId,
    LiveSupportAIWorkerDecisionDto Decision,
    string DecisionHash,
    string CallbackIdempotencyKey,
    string Provider,
    string Model,
    string? ProviderResponseId,
    int? InputTokenCount,
    int? OutputTokenCount,
    int LatencyMs);

public sealed record LiveSupportAIWorkerFailureDto(
    string FailureCode,
    string CallbackIdempotencyKey,
    string? Provider,
    string? Model,
    int LatencyMs);

public sealed record LiveSupportAIPendingDecisionDto(
    Guid Id,
    LiveSupportAIPendingDecisionKind Kind,
    string ActionKey,
    string SafeProposalJson,
    LiveSupportAIPendingActionStatus Status,
    DateTime ExpiresAt,
    string? FailureCode);

public sealed record LiveSupportAIVerificationStateDto(
    Guid SessionId,
    LiveSupportAIVerificationStatus Status,
    string? PromptText,
    int AttemptCount,
    int MaxAttempts);

public sealed record LiveSupportAIParticipantSnapshotDto(
    Guid ConversationId,
    string Status,
    LiveSupportAIMode? AiMode,
    long LastSequence,
    bool CanSend,
    string? AiTurnState,
    LiveSupportAIPendingDecisionDto? PendingDecision,
    LiveSupportAIVerificationStateDto? Verification,
    int? QueuePosition,
    IReadOnlyList<object> Messages);

public sealed record LiveSupportAIVerificationLookupCommandDto(string LookupKey, string Value, string IdempotencyKey);
public sealed record LiveSupportAIVerificationAnswerCommandDto(Guid SessionId, string Answer, string IdempotencyKey);
public sealed record LiveSupportAIConfirmationCommandDto(Guid DecisionId, string IdempotencyKey);
public sealed record LiveSupportAIPreviewRequestDto(Guid? PolicyVersionId, string Message);

public sealed record LiveSupportAISecureRegistrationDto(
    Guid DecisionId,
    string IdempotencyKey,
    string FullName,
    string PhoneNumber,
    string Password,
    DateTime DateOfBirth,
    string Gender,
    string Governorate,
    string Address,
    string EducationStage,
    string GradeLevel,
    string SchoolName,
    string ParentPhoneNumber);

public sealed record LiveSupportAIRecoveryBatchResultDto(
    int StaleTurns,
    int ExpiredDecisions,
    int ExpiredVerifications,
    int InactivityWarnings,
    int ReconciledConversations);
