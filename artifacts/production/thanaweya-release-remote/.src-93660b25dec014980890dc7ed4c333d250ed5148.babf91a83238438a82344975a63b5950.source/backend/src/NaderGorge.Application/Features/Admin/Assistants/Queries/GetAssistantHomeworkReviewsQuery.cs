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
public record AssistantHomeworkReviewDto(
    Guid Id,
    string StudentName,
    string LessonTitle,
    decimal OverallScore,
    string? AssistantNotes,
    string Status,
    DateTime? GradedAt
);

public record AssistantHomeworkReviewsPagedResult(
    List<AssistantHomeworkReviewDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ── Query ────────────────────────────────────────────────────────────────
public class GetAssistantHomeworkReviewsQuery : IRequest<AssistantHomeworkReviewsPagedResult>
{
    public Guid UserId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    public GetAssistantHomeworkReviewsQuery(Guid userId, int page = 1, int pageSize = 20)
    {
        UserId = userId;
        Page = page;
        PageSize = pageSize;
    }
}

// ── Handler ──────────────────────────────────────────────────────────────
public class GetAssistantHomeworkReviewsQueryHandler
    : IRequestHandler<GetAssistantHomeworkReviewsQuery, AssistantHomeworkReviewsPagedResult>
{
    private readonly IAppDbContext _context;

    public GetAssistantHomeworkReviewsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<AssistantHomeworkReviewsPagedResult> Handle(
        GetAssistantHomeworkReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.HomeworkSubmissions
            .Where(s => s.AssistantReviewerId == request.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.GradedAt ?? s.SubmittedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new AssistantHomeworkReviewDto(
                s.Id,
                s.Student.FullName,
                // Homework → LessonId → join with Lessons to get title
                _context.Lessons
                    .Where(l => l.Id == s.Homework.LessonId)
                    .Select(l => l.Title)
                    .FirstOrDefault() ?? "غير محدد",
                s.OverallScore,
                s.AssistantNotes,
                s.Status.ToString(),
                s.GradedAt
            ))
            .ToListAsync(cancellationToken);

        return new AssistantHomeworkReviewsPagedResult(items, totalCount, request.Page, request.PageSize);
    }
}
