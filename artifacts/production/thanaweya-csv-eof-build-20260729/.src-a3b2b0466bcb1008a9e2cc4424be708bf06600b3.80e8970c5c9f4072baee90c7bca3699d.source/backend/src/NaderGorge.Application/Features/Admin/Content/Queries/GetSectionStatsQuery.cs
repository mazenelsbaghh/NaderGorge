using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Content.Queries;

public record GetSectionStatsQuery(Guid SectionId) : IRequest<ApiResponse<SectionStatsDto>>;

public record SectionStatsDto(
    int LessonsCount,
    int VideosCount,
    long TotalWatchTimeSeconds,
    int TotalWatchSessions
);

public class GetSectionStatsQueryHandler : IRequestHandler<GetSectionStatsQuery, ApiResponse<SectionStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetSectionStatsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<SectionStatsDto>> Handle(GetSectionStatsQuery request, CancellationToken ct)
    {
        var sectionExists = await _db.ContentSections.AnyAsync(cs => cs.Id == request.SectionId, ct);
        if (!sectionExists)
            return ApiResponse<SectionStatsDto>.Fail("Section not found");

        var lessonsCount = await _db.Lessons
            .CountAsync(l => l.ContentSectionId == request.SectionId, ct);

        var lessonIds = await _db.Lessons
            .Where(l => l.ContentSectionId == request.SectionId)
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

        var dto = new SectionStatsDto(
            lessonsCount,
            videosCount,
            totalWatchTimeSeconds,
            totalWatchSessions
        );

        return ApiResponse<SectionStatsDto>.Ok(dto);
    }
}
