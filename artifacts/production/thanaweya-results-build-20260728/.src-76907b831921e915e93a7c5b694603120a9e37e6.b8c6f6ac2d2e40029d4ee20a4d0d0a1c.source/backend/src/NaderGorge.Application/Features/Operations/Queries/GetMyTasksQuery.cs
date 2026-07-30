using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Operations.Queries;

public record GetMyTasksQuery(Guid UserId) : IRequest<ApiResponse<List<TaskDto>>>;

public class GetMyTasksQueryHandler : IRequestHandler<GetMyTasksQuery, ApiResponse<List<TaskDto>>>
{
    private readonly IAppDbContext _db;

    public GetMyTasksQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<TaskDto>>> Handle(GetMyTasksQuery request, CancellationToken ct)
    {
        var tasks = await _db.TaskItems
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Include(t => t.ApprovedBy)
            .Where(t => t.AssigneeId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var dtos = tasks.Select(t => new TaskDto(
            t.Id,
            t.Title,
            t.Description,
            t.AssigneeId,
            t.Assignee?.FullName ?? "Unknown",
            t.CreatedById,
            t.CreatedBy?.FullName ?? "Unknown",
            TaskDto.GetDynamicStatus(t.Status, t.DueDate),
            t.Priority,
            t.DueDate,
            t.CompletedAt,
            t.ApprovedById,
            t.ApprovedBy?.FullName,
            t.CreatedAt,
            t.UpdatedAt
        )).ToList();

        return ApiResponse<List<TaskDto>>.Ok(dtos);
    }
}
