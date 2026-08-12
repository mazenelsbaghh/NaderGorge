using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.AdminAI;

public sealed class AdminAITurn : BaseEntity
{
    public Guid ConversationId { get; set; }
    public AdminAIConversation Conversation { get; set; } = null!;
    public Guid SourceMessageId { get; set; }
    public Guid? OutputMessageId { get; set; }
    public Guid ActorAdminUserId { get; set; }
    public Guid CapabilityBaselineId { get; set; }
    public Guid SensitiveDataPolicyVersionId { get; set; }
    public long ExpectedConversationVersion { get; set; }
    public long ExpectedSecurityVersion { get; set; }
    public AdminAITurnStatus Status { get; set; } = AdminAITurnStatus.Queued;
    public int CurrentStepNumber { get; set; }
    public int ReadInvocationCount { get; set; }
    public int RedactedContextBytes { get; set; }
    public DateTime? CancellationRequestedAt { get; set; }
    public string CallbackIdempotencyDigest { get; set; } = string.Empty;
    public string? AdmissionPayloadHash { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? ProviderResponseId { get; set; }
    public int? InputTokenCount { get; set; }
    public int? OutputTokenCount { get; set; }
    public string? FailureCode { get; set; }
    public string? SafeFailureDetail { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long Version { get; set; } = 1;
    public ICollection<AdminAITurnStep> Steps { get; set; } = new List<AdminAITurnStep>();
    public ICollection<AdminAIReadInvocation> ReadInvocations { get; set; } = new List<AdminAIReadInvocation>();
}

public sealed class AdminAITurnStep : BaseEntity
{
    public Guid TurnId { get; set; }
    public AdminAITurn Turn { get; set; } = null!;
    public int StepNumber { get; set; }
    public AdminAITurnStepStatus Status { get; set; }
    public AdminAIModelDecisionType? DecisionType { get; set; }
    public string? CanonicalDecisionHash { get; set; }
    public long ExpectedTurnVersion { get; set; }
    public int ToolCallsRequested { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? ProviderResponseId { get; set; }
    public int? InputTokenCount { get; set; }
    public int? OutputTokenCount { get; set; }
    public int? LatencyMs { get; set; }
    public string? FailureCode { get; set; }
    public string CallbackStatus { get; set; } = "Pending";
    public int CallbackAttemptCount { get; set; }
    public DateTime? NextCallbackAttemptAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class AdminAIReadInvocation : BaseEntity
{
    public Guid TurnId { get; set; }
    public AdminAITurn Turn { get; set; } = null!;
    public Guid TurnStepId { get; set; }
    public int InvocationSequence { get; set; }
    public string CapabilityKey { get; set; } = string.Empty;
    public string CapabilityVersion { get; set; } = string.Empty;
    public string SafeInputJson { get; set; } = "{}";
    public string InputHash { get; set; } = string.Empty;
    public string SafeScopeJson { get; set; } = "{}";
    public AdminAIReadInvocationStatus Status { get; set; } = AdminAIReadInvocationStatus.Pending;
    public int ResultCount { get; set; }
    public bool IsComplete { get; set; }
    public bool IsTruncated { get; set; }
    public DateTime DataAsOf { get; set; }
    public string SafeEvidenceJson { get; set; } = "{}";
    public byte[]? ProtectedResult { get; set; }
    public string? ProtectedResultHash { get; set; }
    public DateTime? ProtectedResultExpiresAt { get; set; }
    public int LatencyMs { get; set; }
    public string? FailureCode { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}
