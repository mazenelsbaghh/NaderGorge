using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
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
    private readonly IAcademicScopeService _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public GetProgressQueryHandler(IAppDbContext db, IAcademicScopeService academicScope, IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _academicScope = academicScope;
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(db);
    }

    public async Task<ApiResponse<ProgressDto>> Handle(GetProgressQuery request, CancellationToken ct)
    {
        var grants = await _db.StudentAccessGrants
            .AsNoTracking()
            .Where(g => g.UserId == request.UserId && g.IsActive)
            .Select(g => new { g.PackageId, g.GrantType })
            .ToListAsync(ct);

        var packageIds = grants
            .Where(g => g.GrantType == Domain.Enums.CodeType.Package && g.PackageId.HasValue)
            .Select(g => g.PackageId!.Value)
            .Distinct()
            .ToList();

        packageIds = await FilterEligibleOwnerIdsAsync(StudentFacingScopeOwnerType.Package, packageIds, request.UserId, ct);

        var packages = await _db.Packages
            .AsNoTracking()
            .Where(p => packageIds.Contains(p.Id))
            .Select(p => new {
                p.Id,
                p.Name,
                Lessons = p.Terms.SelectMany(t => t.Sections).SelectMany(s => s.Lessons).Select(l => new { l.Id, l.Title, l.Order, l.ExamId, l.ContentSectionId }).ToList()
            })
            .ToListAsync(ct);

        var completedLessonIds = await _db.LessonProgresses
            .AsNoTracking()
            .Where(lp => lp.UserId == request.UserId && lp.IsCompleted)
            .Select(lp => lp.LessonId)
            .ToListAsync(ct);

        var manuallyUnlockedIds = await _db.LessonProgresses
            .AsNoTracking()
            .Where(lp => lp.UserId == request.UserId && lp.IsManuallyUnlocked)
            .Select(lp => lp.LessonId)
            .ToListAsync(ct);

        var passedExamIds = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);

        var failedExamCount = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && !a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .CountAsync(ct);

        var allLessonIds = packages.SelectMany(p => p.Lessons).Select(l => l.Id).ToList();

        var mandatoryHomeworks = await _db.Homeworks
            .AsNoTracking()
            .Where(h => allLessonIds.Contains(h.LessonId) && h.IsMandatory)
            .Select(h => new { h.Id, h.LessonId })
            .ToListAsync(ct);
        var visibleMandatoryHomeworkIds = new HashSet<Guid>();
        foreach (var homework in mandatoryHomeworks)
        {
            if (await _archiveAccess.CanViewAsync(request.UserId, ContentArchiveTargetType.Homework, homework.Id, ct))
                visibleMandatoryHomeworkIds.Add(homework.Id);
        }

        var passedHomeworkIds = await _db.HomeworkSubmissions
            .AsNoTracking()
            .Where(s => s.StudentId == request.UserId && s.Status == Domain.Entities.Homework.SubmissionStatus.Graded)
            .Join(_db.Homeworks,
                s => s.HomeworkId,
                h => h.Id,
                (s, h) => new { s.HomeworkId, s.OverallScore, PassingScore = h.PassingScoreThreshold ?? 0 })
            .Where(x => x.OverallScore >= x.PassingScore)
            .Select(x => x.HomeworkId)
            .Distinct()
            .ToListAsync(ct);

        int totalLessons = 0;
        int completedLessons = 0;

        var packageProgressList = new List<PackageProgressDto>();

        foreach (var pkg in packages)
        {
            if (!await _archiveAccess.CanViewAsync(request.UserId, ContentArchiveTargetType.Package, pkg.Id, ct))
                continue;

            var lessonItems = new List<LessonProgressItemDto>();
            var eligibleLessonIds = await FilterEligibleOwnerIdsAsync(
                StudentFacingScopeOwnerType.Lesson,
                pkg.Lessons.Select(l => l.Id).ToList(),
                request.UserId,
                ct);
            var visibleLessonIds = new HashSet<Guid>();
            foreach (var lessonId in eligibleLessonIds)
            {
                if (await _archiveAccess.CanViewAsync(request.UserId, ContentArchiveTargetType.Lesson, lessonId, ct))
                    visibleLessonIds.Add(lessonId);
            }
            var orderedLessons = pkg.Lessons
                .Where(l => visibleLessonIds.Contains(l.Id))
                .OrderBy(l => l.Order)
                .ToList();
            var visibleExamIds = new HashSet<Guid>();
            foreach (var examId in orderedLessons.Where(lesson => lesson.ExamId.HasValue).Select(lesson => lesson.ExamId!.Value).Distinct())
            {
                if (await _archiveAccess.CanViewAsync(request.UserId, ContentArchiveTargetType.Exam, examId, ct))
                    visibleExamIds.Add(examId);
            }

            for (int i = 0; i < orderedLessons.Count; i++)
            {
                var lesson = orderedLessons[i];
                totalLessons++;

                var isCompleted = completedLessonIds.Contains(lesson.Id);
                if (isCompleted) completedLessons++;

                var hasExam = lesson.ExamId.HasValue && visibleExamIds.Contains(lesson.ExamId.Value);
                var examPassed = hasExam && passedExamIds.Contains(lesson.ExamId!.Value);

                // Lesson is locked if:
                // - Previous lesson (in the same section) has a mandatory exam that wasn't passed
                // - OR previous lesson (in the same section) has a mandatory homework that wasn't passed
                // - OR current lesson has a mandatory exam that wasn't passed (and there is a previous lesson in the same section)
                bool isLocked = false;
                var prevLesson = orderedLessons
                    .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order < lesson.Order)
                    .OrderByDescending(l => l.Order)
                    .FirstOrDefault();

                bool blockedByPrevExam = false;
                bool blockedByPrevHomework = false;

                if (prevLesson != null)
                {
                    blockedByPrevExam = prevLesson.ExamId.HasValue
                        && visibleExamIds.Contains(prevLesson.ExamId.Value)
                        && !passedExamIds.Contains(prevLesson.ExamId.Value);

                    blockedByPrevHomework = mandatoryHomeworks
                        .Where(h => h.LessonId == prevLesson.Id && visibleMandatoryHomeworkIds.Contains(h.Id))
                        .Any(h => !passedHomeworkIds.Contains(h.Id));
                }

                bool blockedByCurrentExam = hasExam && !passedExamIds.Contains(lesson.ExamId!.Value);

                if ((blockedByPrevExam || blockedByPrevHomework || blockedByCurrentExam) && !manuallyUnlockedIds.Contains(lesson.Id))
                {
                    isLocked = true;
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

    private async Task<List<Guid>> FilterEligibleOwnerIdsAsync(
        StudentFacingScopeOwnerType ownerType,
        List<Guid> ownerIds,
        Guid userId,
        CancellationToken ct)
    {
        var eligible = new List<Guid>();
        foreach (var ownerId in ownerIds)
        {
            if (await _academicScope.IsOwnerEligibleForStudentAsync(ownerType, ownerId, userId, ct))
                eligible.Add(ownerId);
        }

        return eligible;
    }
}
