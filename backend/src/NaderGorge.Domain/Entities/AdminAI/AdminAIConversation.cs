using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.AdminAI;

public sealed class AdminAIConversation : BaseEntity
{
    public Guid OwnerAdminUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public AdminAIConversationStatus Status { get; set; } = AdminAIConversationStatus.Active;
    public long LastSequence { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
    public long Version { get; set; } = 1;
    public string? CreateIdempotencyDigest { get; set; }
    public string? CreatePayloadHash { get; set; }
    public ICollection<AdminAIMessage> Messages { get; set; } = new List<AdminAIMessage>();
    public ICollection<AdminAITurn> Turns { get; set; } = new List<AdminAITurn>();
}

public sealed class AdminAIMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public AdminAIConversation Conversation { get; set; } = null!;
    public long Sequence { get; set; }
    public AdminAIMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? StructuredContentJson { get; set; }
    public Guid? TurnId { get; set; }
}
