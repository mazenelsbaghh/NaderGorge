using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.AdminAI.Interfaces;

public sealed class AdminAISecureInputGoneException : Exception
{
    public AdminAISecureInputGoneException() : base("Secure input grant is no longer available.") { }
}

public sealed record AdminAIAccessSnapshot(Guid UserId, int SecurityVersion, DateTime CheckedAt);
public sealed record AdminAICapabilityDefinition(string Key, string Version, string Kind, string Risk, string Confirmation, string InputSchema, string OutputSchema, int MaxRows, int MaxBytes, int TimeoutMs, string AuthoritativeOperation, IReadOnlyList<string> RefreshScopes);
public sealed record AdminAIProtectedValue(byte[] Ciphertext, string Digest);

public interface IAdminAIAccessGate { Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion, CancellationToken cancellationToken); }
public interface IAdminAICapabilityRegistry { string BaselineHash { get; } IReadOnlyCollection<AdminAICapabilityDefinition> All { get; } bool TryGet(string key, out AdminAICapabilityDefinition definition); }
public interface IAdminAISensitiveDataPolicy { string PolicyHash { get; } void AssertSafeSchema(Type type); string RedactJson(string json); }
public interface IAdminAIDataProtector { AdminAIProtectedValue Protect(string purpose, ReadOnlySpan<byte> plaintext); byte[] Unprotect(string purpose, AdminAIProtectedValue value); string Digest(string purpose, ReadOnlySpan<byte> value); string NormalizeConfirmationPhrase(string value); }
public interface IAdminAIReadExecutor { Task<object> ExecuteAsync(Guid actorId, AdminAIReadCall call, CancellationToken cancellationToken); }
public sealed record AdminAIReadCapabilityResult(object Data, int ResultCount, bool IsComplete, bool IsTruncated, DateTime DataAsOf, IReadOnlyList<string> References);
public interface IAdminAIReadCapability
{
    string Key { get; }
    Type OutputType { get; }
    Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken cancellationToken);
}
public sealed record AdminAIActionSuggestion(string ClientActionId, string CapabilityKey, object Input);
public interface IAdminAIProposalBuilder
{
    Task<AdminAIProposalDto> BuildAsync(Guid actorId, Guid turnId, string capabilityKey, object input, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAIProposalDto>> BuildManyAsync(Guid actorId, Guid turnId, IReadOnlyList<AdminAIActionSuggestion> suggestions, CancellationToken cancellationToken);
}
public interface IAdminAIActionExecutor { Task<AdminAIExecutionResultDto> ExecuteAsync(Guid actorId, Guid proposalId, string idempotencyKey, CancellationToken cancellationToken); }
public sealed record AdminAIActionPreview(string TargetType, string TargetReference, object Current, object Requested, object Effect, object Validation, string StateFingerprint);
public sealed record AdminAIActionItemEvidence(int Sequence, string SafeReference, AdminAIExecutionItemStatus Status, object SafeResult, string? FailureCode = null);
public sealed record AdminAIActionOutcome(
    AdminAIExecutionStatus Status,
    object SafeResult,
    int? AffectedCount,
    IReadOnlyList<string> RefreshScopes,
    Guid? OriginalAuditLogId = null,
    int? SucceededCount = null,
    int? SkippedCount = null,
    int? FailedCount = null,
    IReadOnlyList<AdminAIActionItemEvidence>? Items = null);
public interface IAdminAIActionCapability
{
    string Key { get; }
    Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken cancellationToken);
    Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken cancellationToken);
}
public interface IAdminAISecureActionCapability : IAdminAIActionCapability
{
    string SecureInputKind { get; }
    Task<AdminAIActionOutcome> ExecuteSecureAsync(Guid actorId, object input, ReadOnlyMemory<byte> secureInput, string operationId, CancellationToken cancellationToken);
}
public interface IAdminAIConfirmationChallengeService
{
    Task<string> IssueAsync(Guid actorId, Guid proposalId, string safeActionLabel, CancellationToken cancellationToken);
    Task<string?> PhraseAsync(Guid actorId, Guid proposalId, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(Guid actorId, Guid proposalId, string phrase, CancellationToken cancellationToken);
}
public sealed record AdminAISecureGrantResult(Guid Id, string? Token, string InputKind, AdminAISecureInputGrantStatus Status, DateTime ExpiresAt, long Version);
public interface IAdminAISecureInputService
{
    Task<AdminAISecureGrantResult> IssueAsync(Guid actorId, Guid proposalId, string inputKind, long expectedProposalVersion, CancellationToken cancellationToken);
    Task<AdminAISecureGrantResult> SubmitAsync(Guid actorId, Guid grantId, string token, string inputKind, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    Task<AdminAIProtectedValue> ConsumeAsync(Guid actorId, Guid proposalId, CancellationToken cancellationToken);
}
public interface IAdminAIRecoveryService { Task<int> ReconcileAsync(int batchSize, CancellationToken cancellationToken); }
public interface IAdminAIExternalResultResolver
{
    string CapabilityKey { get; }
    Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId, CancellationToken cancellationToken);
}
public interface IAdminAIExternalOperationReconciler { Task<int> ReconcileAsync(int batchSize, CancellationToken cancellationToken); }
public interface IAdminAIAuditWriter { Task WriteAsync(string eventType, Guid? actorId, Guid? conversationId, Guid? turnId, Guid? proposalId, object safeEvidence, CancellationToken cancellationToken); }
public interface IAdminAITurnOrchestrator
{
    Task<AdminAITurnDto> QueueAsync(Guid actorId, Guid conversationId, string content, long expectedVersion, string idempotencyKey, CancellationToken cancellationToken);
    Task<AdminAITurnDto> CancelAsync(Guid actorId, Guid conversationId, Guid turnId, long expectedVersion, CancellationToken cancellationToken);
}
public sealed record AdminAITurnCompletionResult(Guid TurnId, AdminAITurnStatus Status, long TurnVersion, IReadOnlyList<Guid> ProposalIds, bool Replayed, bool Discarded);
public interface IAdminAITurnCompletionService
{
    Task<AdminAITurnCompletionResult> CompleteAsync(Guid turnId, AdminAIInternalCompleteRequest request, CancellationToken cancellationToken);
}
public interface IAdminAIConversationService
{
    Task<AdminAIConversationSummary> CreateAsync(Guid actorId, string? title, string idempotencyKey, CancellationToken cancellationToken);
    Task<AdminAIConversationSummary> RenameAsync(Guid actorId, Guid conversationId, string title, long expectedVersion, string idempotencyKey, CancellationToken cancellationToken);
    Task<AdminAIConversationSummary> SetArchivedAsync(Guid actorId, Guid conversationId, bool archived, long expectedVersion, string idempotencyKey, CancellationToken cancellationToken);
    Task<AdminAIConversationPage> ListAsync(Guid actorId, AdminAIConversationStatus? status, string? cursor, int pageSize, CancellationToken cancellationToken);
    Task<AdminAIConversationSnapshot> SnapshotAsync(Guid actorId, Guid conversationId, long? beforeSequence, int pageSize, CancellationToken cancellationToken);
}
