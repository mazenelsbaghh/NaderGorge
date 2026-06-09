using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Domain.Entities;

public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public Guid CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.New;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Guid? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }

    public Guid? MediaPipelineId { get; set; }
    public MediaProductionPipeline? MediaPipeline { get; set; }

    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
}
