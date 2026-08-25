using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record MindmapDto(
    string Title,
    string ImageUrl,
    Guid? ChapterId = null,
    int? Order = null);

public record MindmapsCompletedCommand(
    Guid VideoId,
    List<MindmapDto> Mindmaps,
    Guid? GenerationRunId = null) : IRequest<ApiResponse<AiCallbackReceipt>>;

public class MindmapsCompletedCommandHandler : IRequestHandler<MindmapsCompletedCommand, ApiResponse<AiCallbackReceipt>>
{
    private readonly IAppDbContext _db;

    public MindmapsCompletedCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<AiCallbackReceipt>> Handle(MindmapsCompletedCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos
            .Include(v => v.VideoChapters)
            .FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);

        if (video == null) return ApiResponse<AiCallbackReceipt>.Fail("Video not found");

        var targets = ResolveTargets(
            video.VideoChapters,
            request.Mindmaps,
            request.GenerationRunId);
        var isAlreadyRetained =
            !video.IsProcessingMindmaps &&
            !video.CurrentMindmapGenerationRunId.HasValue &&
            targets != null &&
            targets.All(target => string.Equals(
                target.Chapter.MindmapImageUrl,
                target.Mindmap.ImageUrl,
                StringComparison.Ordinal));
        if (isAlreadyRetained)
            return ApiResponse<AiCallbackReceipt>.Ok(
                new AiCallbackReceipt(true),
                "Mindmap artifacts already retained");

        if (!AiGenerationRunContract.IsCurrent(
                video.CurrentMindmapGenerationRunId,
                request.GenerationRunId,
                video.IsProcessingMindmaps))
            return ApiResponse<AiCallbackReceipt>.Ok(
                new AiCallbackReceipt(false),
                "Stale mindmap callback ignored");

        if (!video.IsProcessingMindmaps)
        {
            return ApiResponse<AiCallbackReceipt>.Ok(
                new AiCallbackReceipt(false),
                "Mindmaps already processed");
        }

        if (targets == null)
            return ApiResponse<AiCallbackReceipt>.Fail(
                "Mindmap callback must identify every chapter exactly once.");

        foreach (var target in targets)
        {
            target.Chapter.MindmapImageUrl = target.Mindmap.ImageUrl;
        }

        video.IsProcessingMindmaps = false;
        video.CurrentMindmapGenerationRunId = null;

        var teacherUserId = await _db.LessonVideos
            .Where(candidate => candidate.Id == video.Id)
            .Select(candidate => (string?)candidate.Lesson.ContentSection.Term.Package.Teacher.UserId.ToString())
            .SingleAsync(ct);
        var completedPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            jobId = $"{video.Id}_mindmaps",
            lessonVideoId = video.Id
        });
        _db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "AiJobCompleted",
            TargetGroup = "Role_Admin",
            PayloadJson = completedPayload
        });
        if (teacherUserId != null)
        {
            _db.OutboxEvents.Add(new OutboxEvent
            {
                Type = "AiJobCompleted",
                TargetUserId = teacherUserId,
                PayloadJson = completedPayload
            });
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<AiCallbackReceipt>.Ok(
            new AiCallbackReceipt(true),
            "Mindmaps updated successfully");
    }

    private static IReadOnlyList<MindmapTarget>? ResolveTargets(
        ICollection<VideoChapter> chapters,
        IReadOnlyCollection<MindmapDto> mindmaps,
        Guid? generationRunId)
    {
        if (mindmaps.Count == 0 || mindmaps.Count != chapters.Count)
            return null;

        return generationRunId.HasValue
            ? ResolveFencedTargets(chapters, mindmaps)
            : ResolveLegacyTargets(chapters, mindmaps);
    }

    private static IReadOnlyList<MindmapTarget>? ResolveFencedTargets(
        ICollection<VideoChapter> chapters,
        IReadOnlyCollection<MindmapDto> mindmaps)
    {
        if (mindmaps.Any(mindmap => !mindmap.ChapterId.HasValue ||
                                   !mindmap.Order.HasValue ||
                                   mindmap.Order.Value is < 0 or > 999_999) ||
            mindmaps.Select(mindmap => mindmap.ChapterId).Distinct().Count() != mindmaps.Count ||
            mindmaps.Select(mindmap => mindmap.Order).Distinct().Count() != mindmaps.Count)
            return null;

        var chapterById = chapters.ToDictionary(chapter => chapter.Id);
        var targets = new List<MindmapTarget>(mindmaps.Count);
        foreach (var mindmap in mindmaps.OrderBy(mindmap => mindmap.Order))
        {
            if (!chapterById.TryGetValue(mindmap.ChapterId!.Value, out var chapter))
                return null;
            targets.Add(new MindmapTarget(chapter, mindmap));
        }
        return targets;
    }

    private static IReadOnlyList<MindmapTarget>? ResolveLegacyTargets(
        ICollection<VideoChapter> chapters,
        IReadOnlyCollection<MindmapDto> mindmaps)
    {
        if (chapters.Select(chapter => chapter.Title).Distinct(StringComparer.Ordinal).Count() != chapters.Count ||
            mindmaps.Select(mindmap => mindmap.Title).Distinct(StringComparer.Ordinal).Count() != mindmaps.Count)
            return null;

        var chapterByTitle = chapters.ToDictionary(chapter => chapter.Title, StringComparer.Ordinal);
        var targets = new List<MindmapTarget>(mindmaps.Count);
        foreach (var mindmap in mindmaps)
        {
            if (!chapterByTitle.TryGetValue(mindmap.Title, out var chapter))
                return null;
            targets.Add(new MindmapTarget(chapter, mindmap));
        }
        return targets;
    }

    private sealed record MindmapTarget(VideoChapter Chapter, MindmapDto Mindmap);
}
