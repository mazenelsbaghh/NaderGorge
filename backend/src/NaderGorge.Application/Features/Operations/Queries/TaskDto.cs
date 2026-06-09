using NaderGorge.Domain.Enums;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Features.Operations.Queries;

public record TaskDto(
    Guid Id,
    string Title,
    string Description,
    Guid AssigneeId,
    string AssigneeName,
    Guid CreatedById,
    string CreatedByName,
    TaskStatus Status,
    TaskPriority Priority,
    DateTime? DueDate,
    DateTime? CompletedAt,
    Guid? ApprovedById,
    string? ApprovedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt
)
{
    public static TaskStatus GetDynamicStatus(TaskStatus status, DateTime? dueDate)
    {
        if (status != TaskStatus.Completed && dueDate.HasValue && dueDate.Value < DateTime.UtcNow)
        {
            return TaskStatus.Overdue;
        }
        return status;
    }
}
