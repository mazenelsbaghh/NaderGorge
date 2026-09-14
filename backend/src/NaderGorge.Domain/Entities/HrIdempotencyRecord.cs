using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class HrIdempotencyRecord : BaseEntity
{
    public string Scope { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid? ResultEntityId { get; set; }
    public string? ResponseJson { get; set; }
    public DateTime ExpiresAt { get; set; }
}
