using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record LessonCockpitVideoChapterDto(Guid Id, string Title, int StartTime, int EndTime, string SummaryText, string? MindmapImageUrl, bool IsRegeneratingMindmap, int Order);
public record LessonCockpitVideoExamDto(Guid ExamId, string Title, ContentArchiveMode ArchiveMode, DateTime? ArchivedAt);
public record LessonCockpitVideoTypeDto(Guid Id, string Name, bool IsActive);
public record LessonCockpitBunnyLibraryDto(Guid Id, string Name, string LibraryId, bool IsActive, bool ApiKeyConfigured, bool HlsConfigured);
public record LessonCockpitBunnyReplacementDto(Guid AssetId, string Status, int? EncodeProgress);
public record LessonCockpitBunnyReplacementOutcomeDto(Guid AssetId, string Status, string? ErrorMessage, DateTime? RetiredAtUtc);
public record LessonCockpitVideoDto(Guid Id, string InternalCode, string Title, string Provider, string Url, int Order, int MaxWatchCount, bool IsProcessingAI, bool IsProcessingMindmaps, bool IsActive, LessonCockpitVideoTypeDto VideoType, Guid? ExamId = null, List<LessonCockpitVideoExamDto>? Exams = null, List<LessonCockpitVideoChapterDto>? Chapters = null, ContentArchiveMode ArchiveMode = ContentArchiveMode.None, DateTime? ArchivedAt = null, LessonCockpitBunnyLibraryDto? BunnyLibrary = null, string? BunnyStatus = null, int? BunnyEncodeProgress = null, LessonCockpitBunnyReplacementDto? PendingBunnyReplacement = null, LessonCockpitBunnyReplacementOutcomeDto? LastBunnyReplacementOutcome = null, BunnyPlaybackMode BunnyPlaybackMode = BunnyPlaybackMode.BunnyPlayer);
public record LessonCockpitResourceDto(Guid Id, string Title, string FileUrl, string ResourceType, ContentArchiveMode ArchiveMode, DateTime? ArchivedAt);
public record LessonCockpitHomeworkDto(Guid Id, string Title, bool IsMandatory, bool IsActive, int QuestionCount, decimal? PassingScoreThreshold, ContentArchiveMode ArchiveMode, DateTime? ArchivedAt);
public record LessonCockpitCommentSummaryDto(int Total, int Pending, int Approved, int Rejected);

public record LessonCockpitDto(
    Guid LessonId,
    string InternalCode,
    string Title,
    string Summary,
    Guid? ExamId,
    decimal Price,
    int Order,
    ContentArchiveMode ArchiveMode,
    DateTime? ArchivedAt,
    ContentArchiveMode ExamArchiveMode,
    DateTime? ExamArchivedAt,
    List<LessonCockpitVideoDto> Videos,
    List<LessonCockpitResourceDto> Resources,
    List<LessonCockpitHomeworkDto> Homework,
    LessonCockpitCommentSummaryDto CommentsSummary,
    DateOnly? HomeworkComingSoonOn
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
            .Include(l => l.Videos)
                .ThenInclude(v => v.VideoType)
            .Include(l => l.Videos)
                .ThenInclude(v => v.BunnyStreamLibrary)
            .Include(l => l.Videos)
                .ThenInclude(v => v.BunnyVideoAssets)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);

        if (lesson == null)
            return ApiResponse<LessonCockpitDto>.Fail("Lesson not found");

        // Fetch homework separately as it's not a direct navigation property on the same aggregate root if not configured,
        // Wait, Homework has LessonId. We can query it.
        var homeworks = await _db.Homeworks
            .Where(h => h.LessonId == request.LessonId)
            .Select(h => new LessonCockpitHomeworkDto(h.Id, h.Title, h.IsMandatory, h.IsActive, h.Questions.Count, h.PassingScoreThreshold, h.ArchiveMode, h.ArchivedAt))
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

        var videoIds = lesson.Videos.Select(v => v.Id).ToList();
        var videoExams = await _db.Exams
            .Where(e => videoIds.Contains(e.LessonVideoId ?? Guid.Empty) || (e.LessonVideoId == null && lesson.Videos.Select(v => v.ExamId).Contains(e.Id)))
            .Select(e => new { e.Id, e.Title, e.LessonVideoId, e.ArchiveMode, e.ArchivedAt })
            .ToListAsync(ct);

        var lessonExamArchive = lesson.ExamId.HasValue
            ? await _db.Exams.AsNoTracking()
                .Where(e => e.Id == lesson.ExamId.Value)
                .Select(e => new { e.ArchiveMode, e.ArchivedAt })
                .FirstOrDefaultAsync(ct)
            : null;

        var dto = new LessonCockpitDto(
            lesson.Id,
            lesson.InternalCode,
            lesson.Title,
            lesson.Summary,
            lesson.ExamId,
            lesson.Price,
            lesson.Order,
            lesson.ArchiveMode,
            lesson.ArchivedAt,
            lessonExamArchive?.ArchiveMode ?? ContentArchiveMode.None,
            lessonExamArchive?.ArchivedAt,
            lesson.Videos.OrderBy(v => v.Order).Select(v =>
            {
                var chapters = v.VideoChapters?.OrderBy(c => c.Order)
                    .Select(c => new LessonCockpitVideoChapterDto(c.Id, c.Title, c.StartTime, c.EndTime, c.SummaryText, c.MindmapImageUrl, c.IsRegeneratingMindmap, c.Order))
                    .ToList();

                var examsForVideo = videoExams
                    .Where(e => e.LessonVideoId == v.Id || (e.LessonVideoId == null && v.ExamId == e.Id))
                    .Select(e => new LessonCockpitVideoExamDto(e.Id, e.Title, e.ArchiveMode, e.ArchivedAt))
                    .ToList();

                var currentBunnyAsset = v.BunnyVideoAssets
                    .SingleOrDefault(asset => asset.SourceState == BunnyVideoAssetSourceState.Current);
                var pendingBunnyReplacement = v.BunnyVideoAssets
                    .SingleOrDefault(asset => asset.SourceState == BunnyVideoAssetSourceState.PendingReplacement);
                // Candidates are created sequentially (one pending candidate per logical
                // video). A current asset created after a retired failure therefore
                // represents a successful later replacement, so that earlier outcome
                // must not remain as a misleading active warning in the cockpit.
                var currentBunnySourceStartedAt = currentBunnyAsset?.CreatedAt;
                var lastBunnyReplacementOutcome = v.BunnyVideoAssets
                    .Where(asset => asset.SourceState == BunnyVideoAssetSourceState.Retired
                        && asset.Status is "Failed" or "Expired" or "Cancelled" or "Unknown"
                        && asset.OutcomeSupersededAtUtc == null
                        && (!currentBunnySourceStartedAt.HasValue
                            || (asset.RetiredAtUtc ?? asset.UpdatedAt ?? asset.CreatedAt)
                                > currentBunnySourceStartedAt.Value))
                    .OrderByDescending(asset => asset.RetiredAtUtc ?? asset.UpdatedAt ?? asset.CreatedAt)
                    .FirstOrDefault();

                return new LessonCockpitVideoDto(
                    v.Id,
                    v.InternalCode,
                    v.Title,
                    v.Provider,
                    v.ProviderVideoId,
                    v.Order,
                    v.MaxWatchCount,
                    v.IsProcessingAI,
                    v.IsProcessingMindmaps,
                    v.IsActive,
                    new LessonCockpitVideoTypeDto(v.VideoType.Id, v.VideoType.Name, v.VideoType.IsActive),
                    v.ExamId,
                    examsForVideo,
                    chapters,
                    v.ArchiveMode,
                    v.ArchivedAt,
                    v.BunnyStreamLibrary is null
                        ? null
                        : new LessonCockpitBunnyLibraryDto(
                            v.BunnyStreamLibrary.Id,
                            v.BunnyStreamLibrary.Name,
                            v.BunnyStreamLibrary.ExternalLibraryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            v.BunnyStreamLibrary.IsActive,
                            v.BunnyStreamLibrary.ApiKeyCiphertext is { Length: > 0 },
                            v.BunnyStreamLibrary.HlsTokenKeyCiphertext is { Length: > 0 } && v.BunnyStreamLibrary.HlsCdnHostname != null),
                    currentBunnyAsset?.Status,
                    currentBunnyAsset?.BunnyEncodeProgress,
                    pendingBunnyReplacement is null
                        ? null
                        : new LessonCockpitBunnyReplacementDto(
                            pendingBunnyReplacement.Id,
                            pendingBunnyReplacement.Status,
                            pendingBunnyReplacement.BunnyEncodeProgress),
                    lastBunnyReplacementOutcome is null
                        ? null
                        : new LessonCockpitBunnyReplacementOutcomeDto(
                            lastBunnyReplacementOutcome.Id,
                            lastBunnyReplacementOutcome.Status,
                            lastBunnyReplacementOutcome.ErrorMessage,
                            lastBunnyReplacementOutcome.RetiredAtUtc),
                    v.BunnyPlaybackMode
                );
            }).ToList(),
            lesson.Resources.Select(r => new LessonCockpitResourceDto(r.Id, r.Title, r.FileUrl, r.ResourceType, r.ArchiveMode, r.ArchivedAt)).ToList(),
            homeworks,
            commentsSummary,
            lesson.HomeworkComingSoonOn
        );

        return ApiResponse<LessonCockpitDto>.Ok(dto);
    }
}
