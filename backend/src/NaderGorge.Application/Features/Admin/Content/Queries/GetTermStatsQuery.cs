using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Content.Queries;

public record GetTermStatsQuery(Guid TermId) : IRequest<ApiResponse<TermStatsDto>>;

public record TermStatsDto(
    int EnrolledStudentsCount,
    int SectionsCount,
    int LessonsCount,
    int VideosCount,
    long TotalWatchTimeSeconds,
    int TotalWatchSessions
);

public class GetTermStatsQueryHandler : IRequestHandler<GetTermStatsQuery, ApiResponse<TermStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetTermStatsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<TermStatsDto>> Handle(GetTermStatsQuery request, CancellationToken ct)
    {
        var term = await _db.Terms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TermId, ct);

        if (term is null)
            return ApiResponse<TermStatsDto>.Fail("Term not found");

        // Enrolled students: grants directly on this term OR on the parent package
        var enrolledStudentsCount = await _db.StudentAccessGrants
            .Where(sag => sag.IsActive &&
                (sag.TermId == request.TermId || sag.PackageId == term.PackageId))
            .Select(sag => sag.UserId)
            .Distinct()
            .CountAsync(ct);

        var sectionsCount = await _db.ContentSections
            .CountAsync(cs => cs.TermId == request.TermId, ct);

        var sectionIds = await _db.ContentSections
            .Where(cs => cs.TermId == request.TermId)
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

        var dto = new TermStatsDto(
            enrolledStudentsCount,
            sectionsCount,
            lessonsCount,
            videosCount,
            totalWatchTimeSeconds,
            totalWatchSessions
        );

        return ApiResponse<TermStatsDto>.Ok(dto);
    }
}
