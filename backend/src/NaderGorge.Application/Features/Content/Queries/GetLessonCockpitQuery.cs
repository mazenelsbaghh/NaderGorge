using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record LessonCockpitVideoChapterDto(Guid Id, string Title, int StartTime, int EndTime, string SummaryText, string? MindmapImageUrl, int Order);
public record LessonCockpitVideoDto(Guid Id, string Title, string Provider, string Url, int Order, int MaxWatchCount, bool IsProcessingAI, bool IsProcessingMindmaps, List<LessonCockpitVideoChapterDto>? Chapters = null);
public record LessonCockpitResourceDto(Guid Id, string Title, string FileUrl, string ResourceType);
public record LessonCockpitHomeworkDto(Guid Id, string Title, bool IsMandatory, decimal? PassingScoreThreshold);
public record LessonCockpitCommentSummaryDto(int Total, int Pending, int Approved, int Rejected);

public record LessonCockpitDto(
    Guid LessonId,
    string Title,
    string Summary,
    Guid? ExamId,
    decimal Price,
    List<LessonCockpitVideoDto> Videos,
    List<LessonCockpitResourceDto> Resources,
    List<LessonCockpitHomeworkDto> Homework,
    LessonCockpitCommentSummaryDto CommentsSummary
);

public record GetLessonCockpitQuery(Guid LessonId, Guid? CurrentUserId = null) : IRequest<ApiResponse<LessonCockpitDto>>;

public class GetLessonCockpitQueryHandler : IRequestHandler<GetLessonCockpitQuery, ApiResponse<LessonCockpitDto>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public GetLessonCockpitQueryHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<LessonCockpitDto>> Handle(GetLessonCockpitQuery request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccess)
            {
                return ApiResponse<LessonCockpitDto>.Fail("Unauthorized access to this lesson.");
            }
        }

        var lesson = await _db.Lessons
            .Include(l => l.Videos)
                .ThenInclude(v => v.VideoChapters)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);

        if (lesson == null)
            return ApiResponse<LessonCockpitDto>.Fail("Lesson not found");

        // Fetch homework separately as it's not a direct navigation property on the same aggregate root if not configured,
        // Wait, Homework has LessonId. We can query it.
        var homeworks = await _db.Homeworks
            .Where(h => h.LessonId == request.LessonId)
            .Select(h => new LessonCockpitHomeworkDto(h.Id, h.Title, h.IsMandatory, h.PassingScoreThreshold))
            .ToListAsync(ct);

        var commentsSummary = await _db.LessonComments
            .Where(c => c.LessonId == request.LessonId)
            .GroupBy(_ => 1)
            .Select(g => new LessonCockpitCommentSummaryDto(
                g.Count(),
                g.Count(c => c.Status == NaderGorge.Domain.Enums.LessonCommentStatus.Pending),
                g.Count(c => c.Status == NaderGorge.Domain.Enums.LessonCommentStatus.Approved),
                g.Count(c => c.Status == NaderGorge.Domain.Enums.LessonCommentStatus.Rejected)
            ))
            .FirstOrDefaultAsync(ct) ?? new LessonCockpitCommentSummaryDto(0, 0, 0, 0);

        var dto = new LessonCockpitDto(
            lesson.Id,
            lesson.Title,
            lesson.Summary,
            lesson.ExamId,
            lesson.Price,
            lesson.Videos.OrderBy(v => v.Order).Select(v =>
            {
                var chapters = v.VideoChapters?.OrderBy(c => c.Order)
                    .Select(c => new LessonCockpitVideoChapterDto(c.Id, c.Title, c.StartTime, c.EndTime, c.SummaryText, c.MindmapImageUrl, c.Order))
                    .ToList();

                return new LessonCockpitVideoDto(
                    v.Id,
                    v.Title,
                    v.Provider,
                    v.ProviderVideoId,
                    v.Order,
                    v.MaxWatchCount,
                    v.IsProcessingAI,
                    v.IsProcessingMindmaps,
                    chapters
                );
            }).ToList(),
            lesson.Resources.Select(r => new LessonCockpitResourceDto(r.Id, r.Title, r.FileUrl, r.ResourceType)).ToList(),
            homeworks,
            commentsSummary
        );

        return ApiResponse<LessonCockpitDto>.Ok(dto);
    }
}
