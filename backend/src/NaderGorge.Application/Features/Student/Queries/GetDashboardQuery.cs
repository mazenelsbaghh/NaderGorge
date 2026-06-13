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
    int CodesRedeemed,
    string? AvatarSlug
);

public record ActivePackageDto(
    Guid Id, 
    string Name, 
    string Description, 
    int LessonsCompleted, 
    int TotalLessons, 
    int ProgressPercent,
    string? ImageUrl,
    Guid TeacherId,
    string TeacherName,
    string? TeacherProfileImageUrl,
    Guid SubjectId,
    string SubjectName
);
public record ResumePointDto(Guid PackageId, string PackageName, Guid LessonId, string LessonTitle, int LessonOrder);
public record UpcomingExamDto(Guid ExamId, string ExamTitle, string LessonTitle);

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, ApiResponse<DashboardDto>>
{
    private readonly IAppDbContext _db;

    public GetDashboardQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => new { u.FullName, AvatarSlug = u.StudentProfile != null ? u.StudentProfile.AvatarSlug : null })
            .FirstOrDefaultAsync(ct);
        if (user == null) return ApiResponse<DashboardDto>.Fail("User not found");

        // Get all active access grants for user
        var grants = await _db.StudentAccessGrants
            .AsNoTracking()
            .Where(g => g.UserId == request.UserId && g.IsActive)
            .Select(g => new { g.PackageId })
            .ToListAsync(ct);

        var packageIds = grants.Where(g => g.PackageId.HasValue).Select(g => g.PackageId!.Value).Distinct().ToList();

        // Get packages with flat lesson list projected
        var packages = await _db.Packages
            .AsNoTracking()
            .Where(p => packageIds.Contains(p.Id))
            .Select(p => new {
                p.Id,
                p.Name,
                p.Description,
                p.ImageUrl,
                p.TeacherId,
                TeacherName = p.Teacher != null && p.Teacher.User != null ? p.Teacher.User.FullName : "Unknown",
                TeacherProfileImageUrl = p.Teacher != null ? p.Teacher.ProfileImageUrl : null,
                p.SubjectId,
                SubjectName = p.Subject != null ? p.Subject.Name : "Unknown",
                Lessons = p.Terms.SelectMany(t => t.Sections).SelectMany(s => s.Lessons).Select(l => new { l.Id, l.Title, l.Order }).ToList()
            })
            .ToListAsync(ct);

        // Get completed lessons count
        var completedLessonIds = await _db.LessonProgresses
            .AsNoTracking()
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
            var allLessons = pkg.Lessons;
            var completed = allLessons.Count(l => completedLessonIds.Contains(l.Id));
            var total = allLessons.Count;
            totalLessonsAll += total;
            totalCompletedAll += completed;

            var pct = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;
            activePackages.Add(new ActivePackageDto(
                pkg.Id, 
                pkg.Name, 
                pkg.Description, 
                completed, 
                total, 
                pct,
                pkg.ImageUrl,
                pkg.TeacherId,
                pkg.TeacherName,
                pkg.TeacherProfileImageUrl,
                pkg.SubjectId,
                pkg.SubjectName
            ));

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
        var allLessonIds = packages.SelectMany(p => p.Lessons.Select(l => l.Id)).ToList();
        
        var lessonsWithExams = await _db.Lessons
            .AsNoTracking()
            .Where(l => allLessonIds.Contains(l.Id) && l.ExamId != null)
            .Select(l => new { l.Id, l.Title, l.ExamId })
            .ToListAsync(ct);

        var passedExamIds = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);

        var examIdsToCheck = lessonsWithExams
            .Where(l => l.ExamId.HasValue && !passedExamIds.Contains(l.ExamId.Value))
            .Select(l => l.ExamId!.Value)
            .Distinct()
            .ToList();

        var exams = await _db.Exams
            .AsNoTracking()
            .Where(e => examIdsToCheck.Contains(e.Id))
            .Select(e => new { e.Id, e.Title })
            .ToDictionaryAsync(e => e.Id, e => e.Title, ct);

        foreach (var lesson in lessonsWithExams)
        {
            if (lesson.ExamId.HasValue && exams.TryGetValue(lesson.ExamId.Value, out var examTitle))
            {
                upcomingExams.Add(new UpcomingExamDto(lesson.ExamId.Value, examTitle, lesson.Title));
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
            codesRedeemed,
            user.AvatarSlug
        ));
    }
}
