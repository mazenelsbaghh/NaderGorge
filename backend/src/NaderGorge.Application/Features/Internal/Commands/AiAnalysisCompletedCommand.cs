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

public record AiAnalysisCompletedCommand(Guid VideoId, string SubtitleUrl, List<ChapterDto> Chapters, string? JobId = null) : IRequest<ApiResponse>;

public class AiAnalysisCompletedCommandHandler : IRequestHandler<AiAnalysisCompletedCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly ILogger<AiAnalysisCompletedCommandHandler> _logger;

    public AiAnalysisCompletedCommandHandler(IAppDbContext db, ILogger<AiAnalysisCompletedCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApiResponse> Handle(AiAnalysisCompletedCommand request, CancellationToken ct)
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
            return ApiResponse.Fail("Video not found");
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

        // 3. Attach the video as Modified — update only the two fields we care about
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
            // The two fields we actually want to change:
            IsProcessingAI = false,
            SubtitleUrl = request.SubtitleUrl,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.LessonVideos.Attach(trackedVideo);
        _db.LessonVideos.Entry(trackedVideo).Property(v => v.IsProcessingAI).IsModified = true;
        _db.LessonVideos.Entry(trackedVideo).Property(v => v.SubtitleUrl).IsModified = true;
        _db.LessonVideos.Entry(trackedVideo).Property(v => v.UpdatedAt).IsModified = true;

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

        // 5. Single save — no concurrency token on LessonVideo, so no concurrency exception possible
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[AI Callback] Successfully saved AI results for video {VideoId}", request.VideoId);
        return ApiResponse.Ok("AI chapters processed successfully");
    }
}
