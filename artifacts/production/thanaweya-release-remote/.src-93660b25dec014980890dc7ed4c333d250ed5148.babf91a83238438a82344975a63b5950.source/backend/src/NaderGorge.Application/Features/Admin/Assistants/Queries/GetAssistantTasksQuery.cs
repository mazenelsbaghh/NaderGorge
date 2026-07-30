using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Assistants.Queries;

// ── DTO ──────────────────────────────────────────────────────────────────
public record AssistantTaskDto(
    Guid Id,
    string TaskType,
    string StudentName,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record AssistantTasksPagedResult(
    List<AssistantTaskDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ── Query ────────────────────────────────────────────────────────────────
public class GetAssistantTasksQuery : IRequest<AssistantTasksPagedResult>
{
    public Guid UserId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public AssistantTaskStatus? StatusFilter { get; set; }

    public GetAssistantTasksQuery(Guid userId, int page = 1, int pageSize = 20, AssistantTaskStatus? statusFilter = null)
    {
        UserId = userId;
        Page = page;
        PageSize = pageSize;
        StatusFilter = statusFilter;
    }
}

// ── Handler ──────────────────────────────────────────────────────────────
public class GetAssistantTasksQueryHandler
    : IRequestHandler<GetAssistantTasksQuery, AssistantTasksPagedResult>
{
    private readonly IAppDbContext _context;

    public GetAssistantTasksQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<AssistantTasksPagedResult> Handle(
        GetAssistantTasksQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.AssistantTasks
            .Where(t => t.AssignedAssistantId == request.UserId);

        if (request.StatusFilter.HasValue)
        {
            query = query.Where(t => t.Status == request.StatusFilter.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new AssistantTaskDto(
                t.Id,
                t.TaskType.ToString(),
                t.Student.FullName,
                t.Status.ToString(),
                t.CreatedAt,
                t.CompletedAt
            ))
            .ToListAsync(cancellationToken);

        return new AssistantTasksPagedResult(items, totalCount, request.Page, request.PageSize);
    }
}
