using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Features.Operations.Queries;

public record GetAdminTasksQuery(
    string? Search = null,
    Guid? AssigneeId = null,
    TaskStatus? Status = null,
    TaskPriority? Priority = null
) : IRequest<ApiResponse<List<TaskDto>>>;

public class GetAdminTasksQueryHandler : IRequestHandler<GetAdminTasksQuery, ApiResponse<List<TaskDto>>>
{
    private readonly IAppDbContext _db;

    public GetAdminTasksQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<TaskDto>>> Handle(GetAdminTasksQuery request, CancellationToken ct)
    {
        var query = _db.TaskItems
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Include(t => t.ApprovedBy)
            .AsQueryable();

        if (request.AssigneeId.HasValue)
        {
            query = query.Where(t => t.AssigneeId == request.AssigneeId.Value);
        }

        if (request.Priority.HasValue)
        {
            query = query.Where(t => t.Priority == request.Priority.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(searchLower)
                                     || t.Description.ToLower().Contains(searchLower)
                                     || (t.Assignee != null && t.Assignee.FullName.ToLower().Contains(searchLower)));
        }

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

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

        // Filter by dynamic status if requested
        if (request.Status.HasValue)
        {
            dtos = dtos.Where(d => d.Status == request.Status.Value).ToList();
        }

        return ApiResponse<List<TaskDto>>.Ok(dtos);
    }
}
