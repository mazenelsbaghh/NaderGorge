namespace NaderGorge.Domain.Entities;

public class ChatParticipant
{
    public Guid ChatRoomId { get; set; }
    public ChatRoom ChatRoom { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Guid? LastReadMessageId { get; set; }
    public ChatMessage? LastReadMessage { get; set; }
}
