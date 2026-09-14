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
                .Where(tp => tp.TeacherId == packageContext.TeacherUserId.Value && tp.IsActive)
                .OrderByDescending(tp => tp.UploadedAt)
                .Take(1)
                .Select(tp => tp.FileUrl)
                .ToListAsync(ct);
        }

        if (teacherPhotoUrls.Count == 0)
            return ApiResponse.Fail("لا توجد صورة نشطة للمدرس. ارفع صورة واضحة وفعّلها قبل توليد الصور.");

        var generationRunId = Guid.NewGuid();
        var lockRows = await _db.LessonVideos
            .Where(v => v.Id == request.VideoId && !v.IsProcessingAI && !v.IsProcessingMindmaps)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.IsProcessingMindmaps, true)
                .SetProperty(v => v.CurrentMindmapGenerationRunId, generationRunId), ct);

        if (lockRows == 0)
            return ApiResponse.Fail("Video is already processing an AI task.");

        try
        {
            await _cancellations.ClearMindmapCancellationAsync(video.Id);

            var chaptersData = video.VideoChapters
                .OrderBy(chapter => chapter.Order)
                .ThenBy(chapter => chapter.Id)
                .Select(chapter => new
                {
                    chapterId = chapter.Id,
                    title = chapter.Title,
                    summaryText = chapter.SummaryText,
                    order = chapter.Order
                })
                .ToList();
            var visualStyles = MindmapStyleOptions.ValidVisualStyles(request.VisualStyles);
            var teacherStyles = MindmapStyleOptions.ValidTeacherStyles(request.TeacherStyles);

            await _jobEnqueuer.EnqueueJobAsync("ai-mindmaps-queue", "generate-mindmaps", new
            {
                lessonVideoId = video.Id,
                teacherPhotoUrls = teacherPhotoUrls,
                visualStyles,
                teacherStyles,
                chapters = chaptersData,
                outputLanguage = AiOutputLanguageContract.ToWorkerCode(packageContext.AiOutputLanguage),
                generationRunId
            });
        }
        catch
        {
            await _db.LessonVideos
                .Where(v => v.Id == request.VideoId && v.CurrentMindmapGenerationRunId == generationRunId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.IsProcessingMindmaps, false)
                    .SetProperty(v => v.CurrentMindmapGenerationRunId, (Guid?)null), CancellationToken.None);
            throw;
        }

        return ApiResponse.Ok("Mindmap Generation queued successfully");
    }
}
