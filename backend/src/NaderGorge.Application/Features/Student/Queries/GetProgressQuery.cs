using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetProgressQuery(Guid UserId) : IRequest<ApiResponse<ProgressDto>>;

public record ProgressDto(
    List<PackageProgressDto> Packages,
    int TotalLessons,
    int CompletedLessons,
    int OverallPercent,
    int ExamsPassed,
    int ExamsFailed
);

public record PackageProgressDto(Guid Id, string Name, List<LessonProgressItemDto> Lessons);
public record LessonProgressItemDto(Guid Id, string Title, int Order, bool IsCompleted, bool IsLocked, bool HasExam, bool ExamPassed);

public class GetProgressQueryHandler : IRequestHandler<GetProgressQuery, ApiResponse<ProgressDto>>
{
    private readonly IAppDbContext _db;

    public GetProgressQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<ProgressDto>> Handle(GetProgressQuery request, CancellationToken ct)
    {
        var grants = await _db.StudentAccessGrants
            .Where(g => g.UserId == request.UserId && g.IsActive)
            .ToListAsync(ct);

        var packageIds = grants.Where(g => g.PackageId.HasValue).Select(g => g.PackageId!.Value).Distinct().ToList();

        var packages = await _db.Packages
            .Where(p => packageIds.Contains(p.Id))
            .Include(p => p.Sections).ThenInclude(s => s.Lessons)
            .ToListAsync(ct);

        var completedLessonIds = await _db.LessonProgresses
            .Where(lp => lp.UserId == request.UserId && lp.IsCompleted)
            .Select(lp => lp.LessonId)
            .ToListAsync(ct);

        var manuallyUnlockedIds = await _db.LessonProgresses
            .Where(lp => lp.UserId == request.UserId && lp.IsManuallyUnlocked)
            .Select(lp => lp.LessonId)
            .ToListAsync(ct);

        var passedExamIds = await _db.StudentExamAttempts
            .Where(a => a.UserId == request.UserId && a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);

        var failedExamCount = await _db.StudentExamAttempts
            .Where(a => a.UserId == request.UserId && !a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .CountAsync(ct);

        int totalLessons = 0;
        int completedLessons = 0;

        var packageProgressList = new List<PackageProgressDto>();

        foreach (var pkg in packages)
        {
            var lessonItems = new List<LessonProgressItemDto>();
            var orderedLessons = pkg.Sections.SelectMany(s => s.Lessons).OrderBy(l => l.Order).ToList();

            for (int i = 0; i < orderedLessons.Count; i++)
            {
                var lesson = orderedLessons[i];
                totalLessons++;

                var isCompleted = completedLessonIds.Contains(lesson.Id);
                if (isCompleted) completedLessons++;

                var hasExam = lesson.ExamId.HasValue;
                var examPassed = hasExam && passedExamIds.Contains(lesson.ExamId!.Value);

                // Lesson is locked if previous lesson has an exam that wasn't passed (and not manually unlocked)
                bool isLocked = false;
                if (i > 0)
                {
                    var prevLesson = orderedLessons[i - 1];
                    if (prevLesson.ExamId.HasValue && !passedExamIds.Contains(prevLesson.ExamId.Value) && !manuallyUnlockedIds.Contains(lesson.Id))
                    {
                        isLocked = true;
                    }
                }

                lessonItems.Add(new LessonProgressItemDto(lesson.Id, lesson.Title, lesson.Order, isCompleted, isLocked, hasExam, examPassed));
            }

            packageProgressList.Add(new PackageProgressDto(pkg.Id, pkg.Name, lessonItems));
        }

        var overallPct = totalLessons > 0 ? (int)Math.Round((double)completedLessons / totalLessons * 100) : 0;

        return ApiResponse<ProgressDto>.Ok(new ProgressDto(
            packageProgressList,
            totalLessons,
            completedLessons,
            overallPct,
            passedExamIds.Count,
            failedExamCount
        ));
    }
}
