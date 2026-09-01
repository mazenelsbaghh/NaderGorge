using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public class ChapterDto
{
    public string Title { get; set; } = string.Empty;
    public int StartTime { get; set; }
    public int EndTime { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string? MindmapImageUrl { get; set; }
    public int Order { get; set; }
}

public record AiAnalysisCompletedCommand(
    Guid VideoId,
    string SubtitleUrl,
    List<ChapterDto> Chapters,
    string? JobId = null,
    Guid? GenerationRunId = null) : IRequest<ApiResponse<AiCallbackReceipt>>;

public class AiAnalysisCompletedCommandHandler : IRequestHandler<AiAnalysisCompletedCommand, ApiResponse<AiCallbackReceipt>>
{
    private readonly IAppDbContext _db;
    private readonly ILogger<AiAnalysisCompletedCommandHandler> _logger;

    public AiAnalysisCompletedCommandHandler(IAppDbContext db, ILogger<AiAnalysisCompletedCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApiResponse<AiCallbackReceipt>> Handle(AiAnalysisCompletedCommand request, CancellationToken ct)
    {
        _logger.LogInformation("[AI Callback] Processing video {VideoId} — {ChapterCount} chapters incoming",
            request.VideoId, request.Chapters?.Count ?? 0);

        // 1. Load only the video — no children navigation, no tracking
        var video = await _db.LessonVideos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);

        if (video == null)
        {
            _logger.LogWarning("[AI Callback] Video {VideoId} not found", request.VideoId);
            return ApiResponse<AiCallbackReceipt>.Fail("Video not found");
        }

        var isAlreadyRetained =
            !video.IsProcessingAI &&
            !video.CurrentAiAnalysisRunId.HasValue &&
            !string.IsNullOrWhiteSpace(request.SubtitleUrl) &&
            string.Equals(video.SubtitleUrl, request.SubtitleUrl, StringComparison.Ordinal);
        if (isAlreadyRetained)
        {
            _logger.LogInformation(
                "[AI Callback] Analysis artifact for video {VideoId} is already retained",
                request.VideoId);
            return ApiResponse<AiCallbackReceipt>.Ok(
                new AiCallbackReceipt(true),
                "AI analysis artifact already retained");
        }

        if (!AiGenerationRunContract.IsCurrent(
                video.CurrentAiAnalysisRunId,
                request.GenerationRunId,
                video.IsProcessingAI))
        {
            _logger.LogInformation(
                "[AI Callback] Ignoring stale analysis callback for video {VideoId}",
                request.VideoId);
            return ApiResponse<AiCallbackReceipt>.Ok(
                new AiCallbackReceipt(false),
                "Stale AI analysis callback ignored");
        }

        // 2. Delete old chapters by fetching them separately (no tracking on the parent video)
        var oldChapters = await _db.VideoChapters
            .Where(vc => vc.LessonVideoId == request.VideoId)
            .ToListAsync(ct);

        if (oldChapters.Count > 0)
        {
            _db.VideoChapters.RemoveRange(oldChapters);
            _logger.LogInformation("[AI Callback] Removing {Count} old chapters for video {VideoId}",
                oldChapters.Count, request.VideoId);
        }

        // Attach a partial entity so unrelated video fields cannot be overwritten by the callback.
        var trackedVideo = new LessonVideo
        {
            Id = video.Id,
            Title = video.Title,
            Provider = video.Provider,
            ProviderVideoId = video.ProviderVideoId,
            Order = video.Order,
            MaxWatchCount = video.MaxWatchCount,
            VideoTag = video.VideoTag,
            LessonId = video.LessonId,
            ExamId = video.ExamId,
            CreatedAt = video.CreatedAt,
            SourceRevision = video.SourceRevision,
            IsProcessingAI = false,
            SubtitleUrl = request.SubtitleUrl,
            UpdatedAt = DateTime.UtcNow,
            CurrentAiAnalysisRunId = video.CurrentAiAnalysisRunId,
        };
        _db.LessonVideos.Attach(trackedVideo);
        trackedVideo.CurrentAiAnalysisRunId = null;
        _db.LessonVideos.Entry(trackedVideo).Property(v => v.IsProcessingAI).IsModified = true;
        _db.LessonVideos.Entry(trackedVideo).Property(v => v.SubtitleUrl).IsModified = true;
        _db.LessonVideos.Entry(trackedVideo).Property(v => v.UpdatedAt).IsModified = true;
        _db.LessonVideos.Entry(trackedVideo).Property(v => v.CurrentAiAnalysisRunId).IsModified = true;

        // 4. Add new chapters
        if (request.Chapters is { Count: > 0 })
        {
            var newChapters = request.Chapters.Select(ch => new VideoChapter
            {
                Id = Guid.NewGuid(),
                Title = ch.Title,
                StartTime = ch.StartTime,
                EndTime = ch.EndTime,
                SummaryText = ch.SummaryText,
                MindmapImageUrl = ch.MindmapImageUrl,
                Order = ch.Order,
                LessonVideoId = request.VideoId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }).ToList();

            await _db.VideoChapters.AddRangeAsync(newChapters, ct);

            _logger.LogInformation("[AI Callback] Adding {Count} new chapters for video {VideoId}",
                newChapters.Count, request.VideoId);
        }

        var teacherUserId = await _db.LessonVideos
            .Where(v => v.Id == request.VideoId)
            .Select(v => (string?)v.Lesson.ContentSection.Term.Package.Teacher.UserId.ToString())
            .FirstOrDefaultAsync(ct);

        var outboxEvent = new OutboxEvent
        {
            Type = "VideoReady",
            TargetGroup = $"Lesson_{video.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                videoId = video.Id,
                lessonId = video.LessonId,
                title = video.Title,
                provider = video.Provider,
                providerVideoId = video.ProviderVideoId
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        var aiJobCompletedEvent = new OutboxEvent
        {
            Type = "AiJobCompleted",
            TargetGroup = "Role_Admin",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                jobId = request.JobId ?? video.Id.ToString(),
                lessonVideoId = video.Id
            })
        };
        _db.OutboxEvents.Add(aiJobCompletedEvent);

        if (teacherUserId != null)
        {
            var teacherCompletedEvent = new OutboxEvent
            {
                Type = "AiJobCompleted",
                TargetUserId = teacherUserId,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    jobId = request.JobId ?? video.Id.ToString(),
                    lessonVideoId = video.Id
                })
            };
            _db.OutboxEvents.Add(teacherCompletedEvent);
        }

        // The run-id concurrency token rolls every chapter change back if a newer run starts before this save.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[AI Callback] Successfully saved AI results for video {VideoId}", request.VideoId);
        return ApiResponse<AiCallbackReceipt>.Ok(
            new AiCallbackReceipt(true),
            "AI chapters processed successfully");
    }
}
