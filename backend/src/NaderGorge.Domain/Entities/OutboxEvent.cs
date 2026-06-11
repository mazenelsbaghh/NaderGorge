using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class OutboxEvent : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string? TargetGroup { get; set; }
    public string? TargetUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
}
