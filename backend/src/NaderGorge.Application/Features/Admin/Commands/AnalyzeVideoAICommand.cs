using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Commands;

public record AnalyzeVideoAICommand(Guid VideoId, Guid AdminId) : IRequest<ApiResponse>;

public class AnalyzeVideoAICommandHandler : IRequestHandler<AnalyzeVideoAICommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IJobEnqueuer _jobEnqueuer;
    private readonly IAiJobCancellationStore _cancellations;

    public AnalyzeVideoAICommandHandler(IAppDbContext db, IJobEnqueuer jobEnqueuer, IAiJobCancellationStore cancellations)
    {
        _db = db;
        _jobEnqueuer = jobEnqueuer;
        _cancellations = cancellations;
    }

    public async Task<ApiResponse> Handle(AnalyzeVideoAICommand request, CancellationToken ct)
    {
        var generationRunId = Guid.NewGuid();
        var lockRows = await _db.LessonVideos
            .Where(v => v.Id == request.VideoId && !v.IsProcessingAI && !v.IsProcessingMindmaps)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.IsProcessingAI, true)
                .SetProperty(v => v.CurrentAiAnalysisRunId, generationRunId), ct);

        if (lockRows == 0)
        {
            var exists = await _db.LessonVideos.AnyAsync(v => v.Id == request.VideoId, ct);
            return exists
                ? ApiResponse.Fail("Video is already processing an AI task.")
                : ApiResponse.Fail("Video not found");
        }

        try
        {
            var video = await _db.LessonVideos
                .Include(v => v.BunnyStreamLibrary)
                .Include(v => v.BunnyVideoAssets)
                .SingleAsync(v => v.Id == request.VideoId, ct);

            await _cancellations.ClearVideoAnalysisCancellationAsync(video.Id);

            var sourceUrl = video.ProviderVideoId;
            string? sourceKind = null;
            if (VideoProviders.Normalize(video.Provider) == VideoProviders.Bunny)
            {
                if (video.BunnyStreamLibrary is null)
                {
                    await ReleaseLockAsync(request.VideoId, generationRunId);
                    return ApiResponse.Fail("هذا الفيديو غير مرتبط بمكتبة Bunny.", ["BUNNY_LIBRARY_MISSING"]);
                }

                var currentBunnyAsset = video.BunnyVideoAssets
                    .SingleOrDefault(asset => asset.SourceState == BunnyVideoAssetSourceState.Current);
                if (currentBunnyAsset is not null
                    && !string.Equals(currentBunnyAsset.Status, "Ready", StringComparison.OrdinalIgnoreCase))
                {
                    await ReleaseLockAsync(request.VideoId, generationRunId);
                    return ApiResponse.Fail("انتظر حتى يكتمل تجهيز فيديو Bunny قبل التحليل.", ["BUNNY_VIDEO_NOT_READY"]);
                }

                // The worker must never receive a Bunny iframe/CDN URL. It asks the
                // platform's internal relay for bytes, which revalidates this run
                // and resolves the owning library credential server-side.
                sourceUrl = "bunny-internal";
                sourceKind = "bunny-internal-original";
            }

            var packageContext = await _db.LessonVideos
                .Where(v => v.Id == video.Id)
                .Select(v => new
                {
                    TeacherUserId = (Guid?)v.Lesson.ContentSection.Term.Package.Teacher.UserId,
                    v.Lesson.ContentSection.Term.Package.AiOutputLanguage
                })
                .SingleAsync(ct);

            var teacherPhotoUrls = new List<string>();
            if (packageContext.TeacherUserId != null)
            {
                teacherPhotoUrls = await _db.TeacherPhotos
                    .Where(tp => tp.TeacherId == packageContext.TeacherUserId.Value)
                    .OrderByDescending(tp => tp.IsActive)
                    .ThenByDescending(tp => tp.UploadedAt)
                    .Select(tp => tp.FileUrl)
                    .ToListAsync(ct);
            }

            await _jobEnqueuer.EnqueueJobAsync("ai-video-queue", "analyze-chapters", new
            {
                lessonVideoId = video.Id,
                sourceUrl = sourceUrl,
                sourceKind,
                teacherPhotoUrls = teacherPhotoUrls,
                outputLanguage = AiOutputLanguageContract.ToWorkerCode(packageContext.AiOutputLanguage),
                generationRunId
            });

            var adminEvent = new OutboxEvent
            {
                Type = "AiJobQueued",
                TargetGroup = "Role_Admin",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lessonVideoId = video.Id,
                    jobType = "analyze-chapters"
                })
            };
            _db.OutboxEvents.Add(adminEvent);

            if (packageContext.TeacherUserId != null)
            {
                var teacherEvent = new OutboxEvent
                {
                    Type = "AiJobQueued",
                    TargetUserId = packageContext.TeacherUserId.Value.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        lessonVideoId = video.Id,
                        jobType = "analyze-chapters"
                    })
                };
                _db.OutboxEvents.Add(teacherEvent);
            }
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await _db.LessonVideos
                .Where(v => v.Id == request.VideoId && v.CurrentAiAnalysisRunId == generationRunId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.IsProcessingAI, false)
                    .SetProperty(v => v.CurrentAiAnalysisRunId, (Guid?)null), CancellationToken.None);
            throw;
        }

        return ApiResponse.Ok("AI Analysis queued successfully");
    }

    private Task<int> ReleaseLockAsync(Guid videoId, Guid generationRunId) =>
        _db.LessonVideos
            .Where(v => v.Id == videoId && v.CurrentAiAnalysisRunId == generationRunId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.IsProcessingAI, false)
                .SetProperty(v => v.CurrentAiAnalysisRunId, (Guid?)null), CancellationToken.None);
}
