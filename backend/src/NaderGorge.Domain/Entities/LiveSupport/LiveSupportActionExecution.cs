using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportActionExecution : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid? StudentUserId { get; set; }
    public Guid StaffUserId { get; set; }
    public string ActionKey { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string SafeRequestJson { get; set; } = "{}";
    public string? SafeResultJson { get; set; }
    public LiveSupportActionStatus Status { get; set; }
    public string? FailureCode { get; set; }
    public Guid? AuditLogId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
