using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class AuthoritativeOperationReceipt : BaseEntity
{
    public string OperationId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public Guid ResultEntityId { get; set; }
}
