using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
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
    private readonly IAcademicScopeService _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public GetParentReportQueryHandler(
        IAppDbContext dbContext,
        IAcademicScopeService? academicScope = null,
        IContentArchiveAccessService? archiveAccess = null)
    {
        _dbContext = dbContext;
        _academicScope = academicScope ?? new AcademicScopeService(dbContext);
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(dbContext);
    }

    public async Task<ApiResponse<ParentReportDto>> Handle(GetParentReportQuery request, CancellationToken cancellationToken)
    {
        var student = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.StudentId, cancellationToken);
        if (student == null)
            return ApiResponse<ParentReportDto>.Fail("Student not found.");

        var statusTracker = await _dbContext.StudentStatusTrackers
            .FirstOrDefaultAsync(t => t.StudentId == request.StudentId, cancellationToken);

        var statusString = statusTracker?.CurrentStatus.ToString() ?? "Unknown";
        var completedLessons = await GetCompletedLessonCountAsync(request.StudentId, cancellationToken);

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

    private async Task<int> GetCompletedLessonCountAsync(Guid studentId, CancellationToken ct)
    {
        var candidateLessonIds = await GetCompletionCandidateLessonIdsAsync(studentId, ct);
        var visibleLessonIds = await GetVisibleLessonIdsAsync(studentId, candidateLessonIds, ct);
        var completionContext = new StudentLessonCompletionContext(
            _dbContext,
            studentId,
            visibleLessonIds);
        var visibleVideoIds = await StudentLessonCompletionReader.GetVisibleActiveVideoIdsAsync(
            completionContext,
            _academicScope,
            _archiveAccess,
            ct);
        return (await StudentLessonCompletionReader.GetCompletedLessonIdsAsync(
            completionContext,
            visibleVideoIds,
            ct)).Count;
    }

    private async Task<List<Guid>> GetCompletionCandidateLessonIdsAsync(
        Guid studentId,
        CancellationToken ct)
    {
        var legacyCompletedLessonIds = await _dbContext.LessonProgresses
            .AsNoTracking()
            .Where(progress => progress.UserId == studentId && progress.IsCompleted)
            .Select(progress => progress.LessonId)
            .ToListAsync(ct);
        var watchedLessonIds = await _dbContext.VideoWatchEvents
            .AsNoTracking()
            .Where(watch => watch.UserId == studentId && watch.WatchCount > 0)
            .Select(watch => watch.LessonVideo.LessonId)
            .Distinct()
            .ToListAsync(ct);

        return legacyCompletedLessonIds.Concat(watchedLessonIds).Distinct().ToList();
    }

    private async Task<List<Guid>> GetVisibleLessonIdsAsync(
        Guid studentId,
        IReadOnlyCollection<Guid> candidateLessonIds,
        CancellationToken ct)
    {
        var academicallyEligibleLessonIds = await _academicScope
            .GetEligibleLessonIdsForStudentAsync(candidateLessonIds, studentId, ct);
        return (await _archiveAccess.GetViewableLessonIdsAsync(
                studentId,
                academicallyEligibleLessonIds,
                ct))
            .ToList();
    }
}
