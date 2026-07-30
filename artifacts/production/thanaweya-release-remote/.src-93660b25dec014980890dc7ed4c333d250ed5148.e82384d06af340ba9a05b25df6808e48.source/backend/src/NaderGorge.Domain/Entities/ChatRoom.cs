using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class ChatRoom : BaseEntity
{
    public string? Name { get; set; }
    public ChatRoomType Type { get; set; }
    public Guid? TaskItemId { get; set; }
    public bool IsArchived { get; set; } = false;
    public Guid CreatedByUserId { get; set; }

    // Navigation properties
    public TaskItem? TaskItem { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ChatParticipant> ChatParticipants { get; set; } = new List<ChatParticipant>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
