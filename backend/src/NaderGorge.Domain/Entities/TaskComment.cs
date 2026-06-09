using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class TaskComment : BaseEntity
{
    public Guid TaskId { get; set; }
    public TaskItem? Task { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}
