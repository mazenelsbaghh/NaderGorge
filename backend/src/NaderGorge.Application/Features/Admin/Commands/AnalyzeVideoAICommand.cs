using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record AnalyzeVideoAICommand(Guid VideoId, Guid AdminId) : IRequest<ApiResponse>;

public class AnalyzeVideoAICommandHandler : IRequestHandler<AnalyzeVideoAICommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IJobEnqueuer _jobEnqueuer;

    public AnalyzeVideoAICommandHandler(IAppDbContext db, IJobEnqueuer jobEnqueuer)
    {
        _db = db;
        _jobEnqueuer = jobEnqueuer;
    }

    public async Task<ApiResponse> Handle(AnalyzeVideoAICommand request, CancellationToken ct)
    {
        var lockRows = await _db.LessonVideos
            .Where(v => v.Id == request.VideoId && !v.IsProcessingAI)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsProcessingAI, true), ct);

        if (lockRows == 0)
        {
            var exists = await _db.LessonVideos.AnyAsync(v => v.Id == request.VideoId, ct);
            return exists
                ? ApiResponse.Fail("Video is already processing AI chapters.")
                : ApiResponse.Fail("Video not found");
        }

        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
        if (video == null)
            return ApiResponse.Fail("Video not found");

        // The URL extraction here assumes standard embed code implies the backend 
        // has access to the raw URL or the FFmpeg extractor can download the video.
        // For standard standard provider extraction we use string extraction if not full remote MP4.

        string sourceUrl = video.ProviderVideoId ?? "https://example.com/mock.mp4";
        // In real life context, if this is a vimeo ID, we'd resolve it to a CDN link.

        var teacherUserId = await _db.LessonVideos
            .Where(v => v.Id == video.Id)
            .Select(v => (Guid?)v.Lesson.ContentSection.Term.Package.Teacher.UserId)
            .FirstOrDefaultAsync(ct);

        var teacherPhotoUrls = new List<string>();
        if (teacherUserId != null)
        {
            teacherPhotoUrls = await _db.TeacherPhotos
                .Where(tp => tp.TeacherId == teacherUserId.Value)
                .OrderByDescending(tp => tp.IsActive)
                .ThenByDescending(tp => tp.UploadedAt)
                .Select(tp => tp.FileUrl)
                .ToListAsync(ct);
        }

        try
        {
            await _jobEnqueuer.EnqueueJobAsync("ai-video-queue", "analyze-chapters", new
            {
                lessonVideoId = video.Id,
                sourceUrl = sourceUrl,
                teacherPhotoUrls = teacherPhotoUrls
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

            if (teacherUserId != null)
            {
                var teacherEvent = new OutboxEvent
                {
                    Type = "AiJobQueued",
                    TargetUserId = teacherUserId.Value.ToString(),
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
                .Where(v => v.Id == request.VideoId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsProcessingAI, false), ct);
            throw;
        }

        return ApiResponse.Ok("AI Analysis queued successfully");
    }
}
