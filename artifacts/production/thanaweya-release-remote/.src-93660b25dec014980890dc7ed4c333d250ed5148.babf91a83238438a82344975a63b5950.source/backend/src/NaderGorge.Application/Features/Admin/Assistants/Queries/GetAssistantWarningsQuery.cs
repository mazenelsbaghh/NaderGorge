using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Assistants.Queries;

// ── DTO ──────────────────────────────────────────────────────────────────
public record AssistantWarningDto(
    Guid Id,
    string StudentName,
    string WarningType,
    string? ResolutionNotes,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record AssistantWarningsPagedResult(
    List<AssistantWarningDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ── Query ────────────────────────────────────────────────────────────────
public class GetAssistantWarningsQuery : IRequest<AssistantWarningsPagedResult>
{
    public Guid UserId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    public GetAssistantWarningsQuery(Guid userId, int page = 1, int pageSize = 20)
    {
        UserId = userId;
        Page = page;
        PageSize = pageSize;
    }
}

// ── Handler ──────────────────────────────────────────────────────────────
public class GetAssistantWarningsQueryHandler
    : IRequestHandler<GetAssistantWarningsQuery, AssistantWarningsPagedResult>
{
    private readonly IAppDbContext _context;

    public GetAssistantWarningsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<AssistantWarningsPagedResult> Handle(
        GetAssistantWarningsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.WarningEvents
            .Where(w => w.ResolvedByAssistantId == request.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(w => new AssistantWarningDto(
                w.Id,
                w.Student.FullName,
                w.Severity.ToString(),
                w.ResolutionNotes,
                w.CreatedAt,
                // WarningEvent has no ResolvedAt field; use CreatedAt when resolved
                w.IsResolved ? w.CreatedAt : (DateTime?)null
            ))
            .ToListAsync(cancellationToken);

        return new AssistantWarningsPagedResult(items, totalCount, request.Page, request.PageSize);
    }
}
