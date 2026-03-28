using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public User? PerformedByUser { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}
