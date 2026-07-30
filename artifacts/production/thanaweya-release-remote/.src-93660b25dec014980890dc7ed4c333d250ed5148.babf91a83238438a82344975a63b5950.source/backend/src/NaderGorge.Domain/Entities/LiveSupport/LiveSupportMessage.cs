using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public LiveSupportSenderType SenderType { get; set; }
    public Guid? SenderUserId { get; set; }
    public Guid? SenderGuestSessionId { get; set; }
    public string ClientMessageId { get; set; } = string.Empty;
    public LiveSupportMessageType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? AttachmentId { get; set; }
    public DateTime SentAt { get; set; }
}
