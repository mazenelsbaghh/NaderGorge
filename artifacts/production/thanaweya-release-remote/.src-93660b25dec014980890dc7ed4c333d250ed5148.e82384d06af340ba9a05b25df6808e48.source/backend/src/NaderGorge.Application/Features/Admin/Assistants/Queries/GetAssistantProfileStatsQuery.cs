using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Assistants.Queries;

// ── DTO ──────────────────────────────────────────────────────────────────
public record AssistantProfileStatsDto(
    int CompletedTasksCount,
    int PendingTasksCount,
    int InReviewTasksCount,
    int HomeworkReviewsCount,
    int WarningsResolvedCount,
    decimal? AverageHomeworkScore
);

// ── Query ────────────────────────────────────────────────────────────────
public class GetAssistantProfileStatsQuery : IRequest<AssistantProfileStatsDto>
{
    public Guid UserId { get; set; }

    public GetAssistantProfileStatsQuery(Guid userId)
    {
        UserId = userId;
    }
}

// ── Handler ──────────────────────────────────────────────────────────────
public class GetAssistantProfileStatsQueryHandler
    : IRequestHandler<GetAssistantProfileStatsQuery, AssistantProfileStatsDto>
{
    private readonly IAppDbContext _context;

    public GetAssistantProfileStatsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<AssistantProfileStatsDto> Handle(
        GetAssistantProfileStatsQuery request,
        CancellationToken cancellationToken)
    {
        var completedCount = await _context.AssistantTasks
            .CountAsync(t => t.AssignedAssistantId == request.UserId
                          && t.Status == AssistantTaskStatus.Done, cancellationToken);

        var pendingCount = await _context.AssistantTasks
            .CountAsync(t => t.AssignedAssistantId == request.UserId
                          && t.Status == AssistantTaskStatus.Open, cancellationToken);

        var inReviewCount = await _context.AssistantTasks
            .CountAsync(t => t.AssignedAssistantId == request.UserId
                          && t.Status == AssistantTaskStatus.InReview, cancellationToken);

        var homeworkReviewsCount = await _context.HomeworkSubmissions
            .CountAsync(s => s.AssistantReviewerId == request.UserId, cancellationToken);

        var warningsResolvedCount = await _context.WarningEvents
            .CountAsync(w => w.ResolvedByAssistantId == request.UserId, cancellationToken);

        // Average score only for submissions that have been graded by this assistant
        var avgScore = await _context.HomeworkSubmissions
            .Where(s => s.AssistantReviewerId == request.UserId
                      && s.Status == Domain.Entities.Homework.SubmissionStatus.Graded)
            .Select(s => (decimal?)s.OverallScore)
            .AverageAsync(cancellationToken);

        return new AssistantProfileStatsDto(
            completedCount,
            pendingCount,
            inReviewCount,
            homeworkReviewsCount,
            warningsResolvedCount,
            avgScore
        );
    }
}
