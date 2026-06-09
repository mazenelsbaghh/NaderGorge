using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Operations.Queries;

public record CommentDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string Content,
    string? AttachmentUrl,
    DateTime CreatedAt
);

public record TaskDetailsDto(
    TaskDto Task,
    List<CommentDto> Comments
);

public record GetTaskDetailsQuery(Guid TaskId, Guid UserId, bool IsAdminOrSupervisor) : IRequest<ApiResponse<TaskDetailsDto>>;

public class GetTaskDetailsQueryHandler : IRequestHandler<GetTaskDetailsQuery, ApiResponse<TaskDetailsDto>>
{
    private readonly IAppDbContext _db;

    public GetTaskDetailsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<TaskDetailsDto>> Handle(GetTaskDetailsQuery request, CancellationToken ct)
    {
        var task = await _db.TaskItems
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Include(t => t.ApprovedBy)
            .Include(t => t.Comments)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(t => t.Id == request.TaskId, ct);

        if (task == null)
        {
            throw new KeyNotFoundException("Task not found.");
        }

        if (!request.IsAdminOrSupervisor && task.AssigneeId != request.UserId && task.CreatedById != request.UserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this task's details.");
        }

        var taskDto = new TaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.AssigneeId,
            task.Assignee?.FullName ?? "Unknown",
            task.CreatedById,
            task.CreatedBy?.FullName ?? "Unknown",
            TaskDto.GetDynamicStatus(task.Status, task.DueDate),
            task.Priority,
            task.DueDate,
            task.CompletedAt,
            task.ApprovedById,
            task.ApprovedBy?.FullName,
            task.CreatedAt,
            task.UpdatedAt
        );

        var commentsDto = task.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.Id,
                c.UserId,
                c.User?.FullName ?? "Unknown",
                c.Content,
                c.AttachmentUrl,
                c.CreatedAt
            ))
            .ToList();

        var details = new TaskDetailsDto(taskDto, commentsDto);
        return ApiResponse<TaskDetailsDto>.Ok(details);
    }
}
