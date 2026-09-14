using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.AdminAI;

public sealed class AdminAIActionExecution : BaseEntity
{
    public Guid ProposalId { get; set; }
    public Guid ActorAdminUserId { get; set; }
    public string CapabilityKey { get; set; } = string.Empty;
    public string CapabilityVersion { get; set; } = string.Empty;
    public string IdempotencyDigest { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string AuthoritativeOperation { get; set; } = string.Empty;
    public AdminAIExecutionStatus Status { get; set; }
    public string SafeResultJson { get; set; } = "{}";
    public int? AffectedCount { get; set; }
    public int? SucceededCount { get; set; }
    public int? SkippedCount { get; set; }
    public int? FailedCount { get; set; }
    public string RefreshScopesJson { get; set; } = "[]";
    public Guid? OriginalAuditLogId { get; set; }
    public string? ExternalOperationId { get; set; }
    public string? FailureCode { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTime ClaimedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long Version { get; set; } = 1;
    public ICollection<AdminAIActionExecutionItem> Items { get; set; } = new List<AdminAIActionExecutionItem>();
}

public sealed class AdminAIActionExecutionItem : BaseEntity
{
    public Guid ExecutionId { get; set; }
    public AdminAIActionExecution Execution { get; set; } = null!;
    public int ItemSequence { get; set; }
    public string SafeItemReference { get; set; } = string.Empty;
    public string ItemReferenceHash { get; set; } = string.Empty;
    public AdminAIExecutionItemStatus Status { get; set; }
    public string SafeResultJson { get; set; } = "{}";
    public string? FailureCode { get; set; }
}
