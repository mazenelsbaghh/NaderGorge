using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Content.Queries;

public record GetPackageStatsQuery(Guid PackageId) : IRequest<ApiResponse<PackageStatsDto>>;

public record PackageStatsDto(
    int EnrolledStudentsCount,
    int TermsCount,
    int SectionsCount,
    int LessonsCount,
    int VideosCount,
    int ExamsCount,
    long TotalWatchTimeSeconds,
    int TotalWatchSessions,
    decimal TotalRevenue
);

public class GetPackageStatsQueryHandler : IRequestHandler<GetPackageStatsQuery, ApiResponse<PackageStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetPackageStatsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PackageStatsDto>> Handle(GetPackageStatsQuery request, CancellationToken ct)
    {
        var packageExists = await _db.Packages.AnyAsync(p => p.Id == request.PackageId, ct);
        if (!packageExists)
            return ApiResponse<PackageStatsDto>.Fail("Package not found");

        // Enrolled students: distinct users with active Package-level access grants
        var enrolledStudentsCount = await _db.StudentAccessGrants
            .Where(sag => sag.GrantType == Domain.Enums.CodeType.Package && sag.PackageId == request.PackageId && sag.IsActive)
            .Select(sag => sag.UserId)
            .Distinct()
            .CountAsync(ct);

        var termsCount = await _db.Terms
            .CountAsync(t => t.PackageId == request.PackageId, ct);

        // Collect term IDs for downstream queries
        var termIds = await _db.Terms
            .Where(t => t.PackageId == request.PackageId)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var sectionsCount = await _db.ContentSections
            .CountAsync(cs => termIds.Contains(cs.TermId), ct);

        var sectionIds = await _db.ContentSections
            .Where(cs => termIds.Contains(cs.TermId))
            .Select(cs => cs.Id)
            .ToListAsync(ct);

        var lessonsCount = await _db.Lessons
            .CountAsync(l => sectionIds.Contains(l.ContentSectionId), ct);

        var lessonIds = await _db.Lessons
            .Where(l => sectionIds.Contains(l.ContentSectionId))
            .Select(l => l.Id)
            .ToListAsync(ct);

        var videosCount = await _db.LessonVideos
            .CountAsync(v => lessonIds.Contains(v.LessonId), ct);

        // Count exams linked to lessons (via ExamId on Lesson)
        var examsCount = await _db.Lessons
            .CountAsync(l => sectionIds.Contains(l.ContentSectionId) && l.ExamId != null, ct);

        // Watch stats: sum across all videos in this package's lessons
        var videoIds = await _db.LessonVideos
            .Where(v => lessonIds.Contains(v.LessonId))
            .Select(v => v.Id)
            .ToListAsync(ct);

        var totalWatchTimeSeconds = videoIds.Count > 0
            ? await _db.VideoWatchEvents
                .Where(vwe => videoIds.Contains(vwe.LessonVideoId))
                .SumAsync(vwe => (long)vwe.TimeWatchedInSeconds, ct)
            : 0L;

        var totalWatchSessions = videoIds.Count > 0
            ? await _db.VideoWatchEvents
                .CountAsync(vwe => videoIds.Contains(vwe.LessonVideoId), ct)
            : 0;

        // Revenue: sum from activation logs linked to this package
        var totalRevenue = await _db.AccessCodeActivationLogs
            .Where(log => log.PackageId == request.PackageId)
            .SumAsync(log => log.Price, ct);

        var dto = new PackageStatsDto(
            enrolledStudentsCount,
            termsCount,
            sectionsCount,
            lessonsCount,
            videosCount,
            examsCount,
            totalWatchTimeSeconds,
            totalWatchSessions,
            totalRevenue
        );

        return ApiResponse<PackageStatsDto>.Ok(dto);
    }
}
