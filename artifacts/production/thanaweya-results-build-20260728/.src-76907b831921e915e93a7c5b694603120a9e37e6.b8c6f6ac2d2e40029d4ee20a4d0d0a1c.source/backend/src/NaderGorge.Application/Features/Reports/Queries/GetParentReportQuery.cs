using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities.Student;

namespace NaderGorge.Application.Features.Reports.Queries;

public record GetParentReportQuery(Guid StudentId) : IRequest<ApiResponse<ParentReportDto>>;

public record ParentReportDto(
    Guid StudentId,
    string StudentName,
    string OverallStatus, // Excellent, AtRisk, etc.
    int CompletedLessonsCount,
    int PassedExamsCount,
    int FailedExamsCount,
    List<WarningDto> RecentWarnings
);

public record WarningDto(string Severity, string Reason, DateTime GeneratedAt);

public class GetParentReportQueryHandler : IRequestHandler<GetParentReportQuery, ApiResponse<ParentReportDto>>
{
    private readonly IAppDbContext _dbContext;

    public GetParentReportQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<ParentReportDto>> Handle(GetParentReportQuery request, CancellationToken cancellationToken)
    {
        var student = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.StudentId, cancellationToken);
        if (student == null)
            return ApiResponse<ParentReportDto>.Fail("Student not found.");

        var statusTracker = await _dbContext.StudentStatusTrackers
            .FirstOrDefaultAsync(t => t.StudentId == request.StudentId, cancellationToken);

        var statusString = statusTracker?.CurrentStatus.ToString() ?? "Unknown";

        var completedLessons = await _dbContext.LessonProgresses
            .CountAsync(lp => lp.UserId == request.StudentId && lp.IsCompleted, cancellationToken);

        var passedExams = await _dbContext.StudentExamAttempts
            .CountAsync(ea => ea.UserId == request.StudentId && ea.IsPassed, cancellationToken);

        var failedExams = await _dbContext.StudentExamAttempts
            .CountAsync(ea => ea.UserId == request.StudentId && !ea.IsPassed, cancellationToken);

        var warnings = await _dbContext.WarningEvents
            .Where(w => w.StudentId == request.StudentId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(5)
            .Select(w => new WarningDto(w.Severity.ToString(), w.TriggerReason, w.CreatedAt))
            .ToListAsync(cancellationToken);

        var dto = new ParentReportDto(
            student.Id,
            student.FullName,
            statusString,
            completedLessons,
            passedExams,
            failedExams,
            warnings
        );

        return ApiResponse<ParentReportDto>.Ok(dto);
    }
}
