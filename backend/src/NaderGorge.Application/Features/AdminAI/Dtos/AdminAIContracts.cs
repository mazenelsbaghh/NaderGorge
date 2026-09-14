using NaderGorge.Domain.Enums;
using System.Text.Json.Serialization;

namespace NaderGorge.Application.Features.AdminAI.Dtos;

public sealed record AdminAIError(string Code, string Message, bool Retryable = false);
public sealed class AdminAIConflictException(string code) : InvalidOperationException
{
    public string Code { get; } = code;
}
public sealed record AdminAIBaselineSummary(string Version, string ManifestHash, string SourceRevision, int ReadCount, int ActionCount, int ExclusionCount, DateTime ActivatedAt);
public sealed record AdminAIConversationSummary(Guid Id, string Title, AdminAIConversationStatus Status, DateTime LastActivityAt, long Version);
public sealed record AdminAIConversationPage(IReadOnlyList<AdminAIConversationSummary> Items, string? NextCursor);
public sealed record AdminAIConversationSnapshot(AdminAIConversationSummary Conversation, IReadOnlyList<AdminAIMessageDto> Messages, AdminAITurnDto? ActiveTurn, bool HasOlderMessages);
public sealed record AdminAIMessageDto(Guid Id, long Sequence, AdminAIMessageRole Role, string Content, object? StructuredContent, Guid? TurnId, DateTime CreatedAt);
public sealed record AdminAITurnDto(Guid Id, AdminAITurnStatus Status, int Step, int Reads, string? FailureCode, DateTime QueuedAt, DateTime? CompletedAt, long Version);
public sealed record AdminAIEvidenceDto(string Scope, int ResultCount, bool Complete, bool Truncated, DateTime DataAsOf, IReadOnlyList<string> References);
public sealed record AdminAIProposalDto(Guid Id, string CapabilityKey, string TargetType, string TargetReference, AdminAIRiskCategory Risk, AdminAIConfirmationType Confirmation, object Current, object Requested, object Effect, DateTime ExpiresAt, AdminAIProposalStatus Status, long Version, string? StrongPhrase = null);
public sealed record AdminAIExecutionResultDto(Guid Id, AdminAIExecutionStatus Status, int? Affected, int? Succeeded, int? Skipped, int? Failed, IReadOnlyList<string> RefreshScopes, string? FailureCode);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record CreateAdminAIConversationRequest(string? Title);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record RenameAdminAIConversationRequest(string Title, long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIExpectedVersionRequest(long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record SendAdminAIMessageRequest([property: JsonPropertyName("message")] string Content, long ExpectedConversationVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record ConfirmAdminAIProposalRequest(string? TypedPhrase, long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record CancelAdminAITurnRequest(long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record IssueAdminAISecureInputRequest(string InputKind, long ExpectedProposalVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record SubmitAdminAISecureInputRequest(string Token, string Kind, string Value);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIInternalClaimRequest(string SchemaVersion, string WorkerInstanceId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIInternalLeaseRenewRequest(string SchemaVersion, string LeaseToken, long ExpectedTurnVersion, string WorkerInstanceId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIInternalReadRequest(
    string SchemaVersion,
    string LeaseToken,
    long ExpectedTurnVersion,
    string ExpectedBaselineVersion,
    string ExpectedSensitivePolicyVersion,
    string BatchIdempotencyKey,
    IReadOnlyList<AdminAIInternalReadCall> Calls);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIInternalReadCall(string CallId, string CapabilityKey, object Arguments);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIReadCall(
    string CapabilityKey,
    string CapabilityVersion,
    object Input,
    Guid? TurnId = null,
    Guid? TurnStepId = null,
    int? InvocationSequence = null,
    string? TraceId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIInternalDecision(string SchemaVersion, AdminAIInternalDecisionType Type, object Payload, string CanonicalHash);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIInternalCompleteRequest(
    string SchemaVersion,
    string LeaseToken,
    long ExpectedTurnVersion,
    int ExpectedStepNumber,
    string ExpectedBaselineVersion,
    string ExpectedSensitivePolicyVersion,
    object Decision,
    string DecisionHash,
    string CallbackIdempotencyKey,
    string Provider,
    string Model,
    string? ProviderResponseId,
    int? InputTokenCount,
    int? OutputTokenCount,
    int LatencyMs);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record AdminAIInternalFailRequest(
    string SchemaVersion,
    string LeaseToken,
    string CallbackIdempotencyKey,
    AdminAIInternalFailureCode FailureCode,
    string? Provider,
    string? Model,
    int LatencyMs);
public sealed record AdminAIRealtimeEnvelope(Guid EventId, long Sequence, string Type, Guid ConversationId, Guid? TurnId, Guid? ProposalId, DateTime OccurredAt);
public sealed record AdminAIAuditEvidenceDto(Guid Id, string EventType, Guid? ActorAdminUserId, Guid? ProposalId, Guid? ExecutionId, string? CapabilityKey, string SafeEvidenceJson, string EvidenceHash, string CorrelationId, DateTime OccurredAt);
public sealed record AdminAIAuditEvidencePage(IReadOnlyList<AdminAIAuditEvidenceDto> Items, string? NextCursor);

[JsonConverter(typeof(JsonStringEnumConverter<AdminAIInternalDecisionType>))]
public enum AdminAIInternalDecisionType { Answer, Clarify, RequestReads, ProposeActions, Refuse }

[JsonConverter(typeof(JsonStringEnumConverter<AdminAIInternalFailureCode>))]
public enum AdminAIInternalFailureCode
{
    AI_PROVIDER_TIMEOUT,
    AI_PROVIDER_FAILURE,
    AI_INVALID_DECISION,
    AI_QUEUE_STALE,
    TOOL_BUDGET_EXCEEDED,
    CALLBACK_UNAVAILABLE,
    CANCELLED
}

public static class AdminAIErrorCodes
{
    public const string AccessDenied = "admin_ai_access_denied";
    public const string StaleState = "admin_ai_stale_state";
    public const string Expired = "admin_ai_expired";
    public const string InvalidConfirmation = "admin_ai_invalid_confirmation";
    public const string CapabilityUnavailable = "admin_ai_capability_unavailable";
    public const string UnsafeInput = "admin_ai_unsafe_input";
    public const string ProviderUnavailable = "admin_ai_provider_unavailable";
    public const string FeatureDisabled = "admin_ai_feature_disabled";
    public const string InvalidRequest = "admin_ai_invalid_request";
    public const string RateLimited = "admin_ai_rate_limited";
    public const string IdempotencyConflict = "admin_ai_idempotency_conflict";
    public const string ActiveTurnExists = "ACTIVE_TURN_EXISTS";
    public const string ActiveTurnLimit = "ACTIVE_TURN_LIMIT";
    public const string TurnNotFound = "TURN_NOT_FOUND";
    public const string TurnNotClaimable = "TURN_NOT_CLAIMABLE";
    public const string TurnLeaseConflict = "TURN_LEASE_CONFLICT";
    public const string TurnLeaseExpired = "TURN_LEASE_EXPIRED";
    public const string TurnCancelled = "TURN_CANCELLED";
    public const string AccessRevoked = "ACCESS_REVOKED";
    public const string BaselineChanged = "BASELINE_CHANGED";
    public const string SensitivePolicyChanged = "SENSITIVE_POLICY_CHANGED";
    public const string StepVersionConflict = "STEP_VERSION_CONFLICT";
    public const string ReadCapabilityNotAllowed = "READ_CAPABILITY_NOT_ALLOWED";
    public const string ReadArgumentsInvalid = "READ_ARGUMENTS_INVALID";
    public const string ReadBudgetExceeded = "READ_BUDGET_EXCEEDED";
    public const string RedactedContextLimit = "REDACTED_CONTEXT_LIMIT";
    public const string ReadTimeout = "READ_TIMEOUT";
    public const string DecisionSchemaInvalid = "DECISION_SCHEMA_INVALID";
    public const string DecisionHashInvalid = "DECISION_HASH_INVALID";
    public const string DecisionEvidenceInvalid = "DECISION_EVIDENCE_INVALID";
    public const string ActionNotAllowed = "ACTION_NOT_ALLOWED";
    public const string IdempotencyPayloadConflict = "IDEMPOTENCY_PAYLOAD_CONFLICT";
    public const string CallbackDiscarded = "CALLBACK_DISCARDED";
    public const string InternalRateLimited = "INTERNAL_RATE_LIMITED";
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        AccessDenied, StaleState, Expired, InvalidConfirmation, CapabilityUnavailable,
        UnsafeInput, ProviderUnavailable, FeatureDisabled, InvalidRequest, RateLimited,
        IdempotencyConflict, ActiveTurnExists, ActiveTurnLimit, TurnNotFound, TurnNotClaimable, TurnLeaseConflict,
        TurnLeaseExpired, TurnCancelled, AccessRevoked, BaselineChanged,
        SensitivePolicyChanged, StepVersionConflict, ReadCapabilityNotAllowed,
        ReadArgumentsInvalid, ReadBudgetExceeded, RedactedContextLimit, ReadTimeout,
        DecisionSchemaInvalid, DecisionHashInvalid, DecisionEvidenceInvalid,
        ActionNotAllowed, IdempotencyPayloadConflict, CallbackDiscarded,
        InternalRateLimited
    };
}
