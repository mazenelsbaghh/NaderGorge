using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands.MindmapOps;

public record GenerateChapterMindmapsCommand(Guid VideoId) : IRequest<ApiResponse>;

public class GenerateChapterMindmapsCommandHandler : IRequestHandler<GenerateChapterMindmapsCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IJobEnqueuer _jobEnqueuer;

    public GenerateChapterMindmapsCommandHandler(IAppDbContext db, IJobEnqueuer jobEnqueuer)
    {
        _db = db;
        _jobEnqueuer = jobEnqueuer;
    }

    public async Task<ApiResponse> Handle(GenerateChapterMindmapsCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos
            .Include(v => v.VideoChapters)
            .FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);

        if (video == null)
            return ApiResponse.Fail("Video not found");


        if (video.VideoChapters == null || !video.VideoChapters.Any())
            return ApiResponse.Fail("Video has no chapters to generate mind maps for. Please extract chapters first.");

        var lockRows = await _db.LessonVideos
            .Where(v => v.Id == request.VideoId && !v.IsProcessingMindmaps)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsProcessingMindmaps, true), ct);

        if (lockRows == 0)
            return ApiResponse.Fail("Video is already processing mind maps.");

        var teacherPhotoUrl = await _db.TeacherPhotos
            .Where(tp => tp.IsActive)
            .OrderByDescending(tp => tp.UploadedAt)
            .Select(tp => tp.FileUrl)
            .FirstOrDefaultAsync(ct);

        var chaptersData = video.VideoChapters.Select(c => new
        {
            title = c.Title,
            summaryText = c.SummaryText,
            order = c.Order
        }).ToList();

        try
        {
            await _jobEnqueuer.EnqueueJobAsync("ai-mindmaps-queue", "generate-mindmaps", new
            {
                lessonVideoId = video.Id,
                teacherPhotoUrl = teacherPhotoUrl,
                chapters = chaptersData
            });
        }
        catch
        {
            await _db.LessonVideos
                .Where(v => v.Id == request.VideoId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsProcessingMindmaps, false), ct);
            throw;
        }

        return ApiResponse.Ok("Mindmap Generation queued successfully");
    }
}
