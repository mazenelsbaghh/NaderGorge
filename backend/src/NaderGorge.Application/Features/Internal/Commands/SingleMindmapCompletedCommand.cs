using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record SingleMindmapCompletedCommand(
    Guid ChapterId,
    string ImageUrl,
    Guid? GenerationRunId = null) : IRequest<ApiResponse<AiCallbackReceipt>>;

public class SingleMindmapCompletedCommandHandler : IRequestHandler<SingleMindmapCompletedCommand, ApiResponse<AiCallbackReceipt>>
{
    private readonly IAppDbContext _db;

    public SingleMindmapCompletedCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<AiCallbackReceipt>> Handle(SingleMindmapCompletedCommand request, CancellationToken ct)
    {
        var chapter = await _db.VideoChapters
            .Include(candidate => candidate.LessonVideo)
            .FirstOrDefaultAsync(c => c.Id == request.ChapterId, ct);

        if (chapter == null)
            return ApiResponse<AiCallbackReceipt>.Fail("Chapter not found.");

        var isAlreadyRetained =
            !chapter.IsRegeneratingMindmap &&
            !chapter.CurrentMindmapGenerationRunId.HasValue &&
            !chapter.LessonVideo.IsProcessingMindmaps &&
            !chapter.LessonVideo.CurrentMindmapGenerationRunId.HasValue &&
            !string.IsNullOrWhiteSpace(request.ImageUrl) &&
            string.Equals(chapter.MindmapImageUrl, request.ImageUrl, StringComparison.Ordinal);
        if (isAlreadyRetained)
            return ApiResponse<AiCallbackReceipt>.Ok(
                new AiCallbackReceipt(true),
                "Single mindmap artifact already retained.");

        var isLegacyRun = !request.GenerationRunId.HasValue &&
            !chapter.CurrentMindmapGenerationRunId.HasValue &&
            chapter.IsRegeneratingMindmap;
        var isCurrentFencedRun = request.GenerationRunId.HasValue &&
            AiGenerationRunContract.IsCurrent(
                chapter.CurrentMindmapGenerationRunId,
                request.GenerationRunId,
                chapter.IsRegeneratingMindmap) &&
            AiGenerationRunContract.IsCurrent(
                chapter.LessonVideo.CurrentMindmapGenerationRunId,
                request.GenerationRunId,
                chapter.LessonVideo.IsProcessingMindmaps);
        if (!isLegacyRun && !isCurrentFencedRun)
            return ApiResponse<AiCallbackReceipt>.Ok(
                new AiCallbackReceipt(false),
                "Stale single mindmap callback ignored.");

        chapter.MindmapImageUrl = request.ImageUrl;
        chapter.IsRegeneratingMindmap = false;
        chapter.CurrentMindmapGenerationRunId = null;
        if (isCurrentFencedRun)
        {
            chapter.LessonVideo.IsProcessingMindmaps = false;
            chapter.LessonVideo.CurrentMindmapGenerationRunId = null;
        }

        var teacherUserId = await _db.LessonVideos
            .Where(video => video.Id == chapter.LessonVideoId)
            .Select(video => (string?)video.Lesson.ContentSection.Term.Package.Teacher.UserId.ToString())
            .SingleAsync(ct);
        var completedPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            jobId = $"{chapter.LessonVideoId}_mindmaps",
            lessonVideoId = chapter.LessonVideoId
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
            "Single mindmap image updated successfully.");
    }
}
