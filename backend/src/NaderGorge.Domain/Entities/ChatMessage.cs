using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid ChatRoomId { get; set; }
    public ChatRoom ChatRoom { get; set; } = null!;

    public Guid SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;

    public string? MediaUrl { get; set; }
    public string? MediaMetadata { get; set; } // JSON metadata

    public bool IsPinned { get; set; } = false;
}
