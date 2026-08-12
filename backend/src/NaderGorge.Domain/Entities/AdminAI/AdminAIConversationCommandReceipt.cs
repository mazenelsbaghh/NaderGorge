using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.AdminAI;

public sealed class AdminAIConversationCommandReceipt : BaseEntity
{
    public Guid OwnerAdminUserId { get; set; }
    public Guid ConversationId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyDigest { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string ResponseTitle { get; set; } = string.Empty;
    public AdminAIConversationStatus ResponseStatus { get; set; }
    public DateTime ResponseLastActivityAt { get; set; }
    public long ResponseVersion { get; set; }
}
