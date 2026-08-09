using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands.MindmapOps;

public record GenerateChapterMindmapsCommand(
    Guid VideoId,
    IReadOnlyCollection<string>? VisualStyles = null,
    IReadOnlyCollection<string>? TeacherStyles = null) : IRequest<ApiResponse>;

public class GenerateChapterMindmapsCommandHandler : IRequestHandler<GenerateChapterMindmapsCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IJobEnqueuer _jobEnqueuer;
    private readonly IAiJobCancellationStore _cancellations;

    public GenerateChapterMindmapsCommandHandler(IAppDbContext db, IJobEnqueuer jobEnqueuer, IAiJobCancellationStore cancellations)
    {
        _db = db;
        _jobEnqueuer = jobEnqueuer;
        _cancellations = cancellations;
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

        await _cancellations.ClearMindmapCancellationAsync(video.Id);

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

        var chaptersData = video.VideoChapters.Select(c => new
        {
            title = c.Title,
            summaryText = c.SummaryText,
            order = c.Order
        }).ToList();
        var visualStyles = MindmapStyleOptions.ValidVisualStyles(request.VisualStyles);
        var teacherStyles = MindmapStyleOptions.ValidTeacherStyles(request.TeacherStyles);

        try
        {
            await _jobEnqueuer.EnqueueJobAsync("ai-mindmaps-queue", "generate-mindmaps", new
            {
                lessonVideoId = video.Id,
                teacherPhotoUrls = teacherPhotoUrls,
                visualStyles,
                teacherStyles,
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
