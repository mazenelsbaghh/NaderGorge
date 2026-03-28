using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetDashboardQuery(Guid UserId) : IRequest<ApiResponse<DashboardDto>>;

public record DashboardDto(
    string StudentName,
    List<ActivePackageDto> ActivePackages,
    ResumePointDto? ResumePoint,
    List<UpcomingExamDto> UpcomingExams,
    int OverallProgressPercent,
    int TotalLessonsCompleted,
    int TotalLessons,
    int CodesRedeemed
);

public record ActivePackageDto(Guid Id, string Name, string Description, int LessonsCompleted, int TotalLessons, int ProgressPercent);
public record ResumePointDto(Guid PackageId, string PackageName, Guid LessonId, string LessonTitle, int LessonOrder);
public record UpcomingExamDto(Guid ExamId, string ExamTitle, string LessonTitle);

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, ApiResponse<DashboardDto>>
{
    private readonly IAppDbContext _db;

    public GetDashboardQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user == null) return ApiResponse<DashboardDto>.Fail("User not found");

        // Get all access grants for user
        var grants = await _db.StudentAccessGrants
            .Where(g => g.UserId == request.UserId && g.IsActive)
            .ToListAsync(ct);

        var packageIds = grants.Where(g => g.PackageId.HasValue).Select(g => g.PackageId!.Value).Distinct().ToList();

        // Get packages with section/lesson counts
        var packages = await _db.Packages
            .Where(p => packageIds.Contains(p.Id))
            .Include(p => p.Terms).ThenInclude(t => t.Sections).ThenInclude(s => s.Lessons)
            .ToListAsync(ct);

        // Get all lesson progress for this user
        var completedLessonIds = await _db.LessonProgresses
            .Where(lp => lp.UserId == request.UserId && lp.IsCompleted)
            .Select(lp => lp.LessonId)
            .ToListAsync(ct);

        var activePackages = new List<ActivePackageDto>();
        Guid? resumePackageId = null;
        string? resumePackageName = null;
        Guid? resumeLessonId = null;
        string? resumeLessonTitle = null;
        int resumeLessonOrder = 0;

        int totalLessonsAll = 0;
        int totalCompletedAll = 0;

        foreach (var pkg in packages)
        {
            var allLessons = pkg.Terms.SelectMany(t => t.Sections).SelectMany(s => s.Lessons).ToList();
            var completed = allLessons.Count(l => completedLessonIds.Contains(l.Id));
            var total = allLessons.Count;
            totalLessonsAll += total;
            totalCompletedAll += completed;

            var pct = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;
            activePackages.Add(new ActivePackageDto(pkg.Id, pkg.Name, pkg.Description, completed, total, pct));

            // Find resume point: first incomplete lesson in order
            if (resumeLessonId == null)
            {
                var orderedLessons = allLessons.OrderBy(l => l.Order).ToList();
                var nextLesson = orderedLessons.FirstOrDefault(l => !completedLessonIds.Contains(l.Id));
                if (nextLesson != null)
                {
                    resumePackageId = pkg.Id;
                    resumePackageName = pkg.Name;
                    resumeLessonId = nextLesson.Id;
                    resumeLessonTitle = nextLesson.Title;
                    resumeLessonOrder = nextLesson.Order;
                }
            }
        }

        // Upcoming exams: lessons with exams that haven't been passed yet
        var upcomingExams = new List<UpcomingExamDto>();
        var allLessonIds = packages.SelectMany(p => p.Terms.SelectMany(t => t.Sections.SelectMany(s => s.Lessons.Select(l => l.Id)))).ToList();
        var lessonsWithExams = await _db.Lessons
            .Where(l => allLessonIds.Contains(l.Id) && l.ExamId != null)
            .ToListAsync(ct);

        var passedExamIds = await _db.StudentExamAttempts
            .Where(a => a.UserId == request.UserId && a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var lesson in lessonsWithExams)
        {
            if (!passedExamIds.Contains(lesson.ExamId!.Value))
            {
                var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == lesson.ExamId!.Value, ct);
                if (exam != null)
                    upcomingExams.Add(new UpcomingExamDto(exam.Id, exam.Title, lesson.Title));
            }
        }

        var codesRedeemed = grants.Count;
        var overallPct = totalLessonsAll > 0 ? (int)Math.Round((double)totalCompletedAll / totalLessonsAll * 100) : 0;

        ResumePointDto? resume = resumeLessonId != null
            ? new ResumePointDto(resumePackageId!.Value, resumePackageName!, resumeLessonId.Value, resumeLessonTitle!, resumeLessonOrder)
            : null;

        return ApiResponse<DashboardDto>.Ok(new DashboardDto(
            user.FullName,
            activePackages,
            resume,
            upcomingExams,
            overallPct,
            totalCompletedAll,
            totalLessonsAll,
            codesRedeemed
        ));
    }
}
