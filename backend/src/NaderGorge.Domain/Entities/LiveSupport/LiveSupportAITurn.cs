using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAITurn : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SourceMessageId { get; set; }
    public Guid PolicyVersionId { get; set; }
    public long ExpectedConversationVersion { get; set; }
    public LiveSupportAITurnStatus Status { get; set; }
    public LiveSupportAIDecisionType? DecisionType { get; set; }
    public Guid? OutputMessageId { get; set; }
    public string ContextCategoryKeysJson { get; set; } = "[]";
    public string KnowledgeRevisionIdsJson { get; set; } = "[]";
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? ProviderResponseId { get; set; }
    public int? InputTokenCount { get; set; }
    public int? OutputTokenCount { get; set; }
    public int? LatencyMs { get; set; }
    public string? FailureCode { get; set; }
    public string? SafeFailureDetail { get; set; }
    public string? DecisionHash { get; set; }
    public LiveSupportAICallbackStatus CallbackStatus { get; set; }
    public int CallbackAttemptCount { get; set; }
    public DateTime? NextCallbackAttemptAt { get; set; }
    public DateTime? ProviderCompletedAt { get; set; }
    public string? LastSafeCallbackErrorCode { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long Version { get; set; }
}
