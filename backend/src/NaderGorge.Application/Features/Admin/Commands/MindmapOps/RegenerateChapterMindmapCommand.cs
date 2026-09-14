using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands.MindmapOps;

public record RegenerateChapterMindmapCommand(
    Guid ChapterId,
    IReadOnlyCollection<string>? VisualStyles = null,
    IReadOnlyCollection<string>? TeacherStyles = null) : IRequest<ApiResponse>;

public class RegenerateChapterMindmapCommandHandler : IRequestHandler<RegenerateChapterMindmapCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IJobEnqueuer _jobEnqueuer;
    private readonly IAiJobCancellationStore _cancellations;

    public RegenerateChapterMindmapCommandHandler(
        IAppDbContext db,
        IJobEnqueuer jobEnqueuer,
        IAiJobCancellationStore cancellations)
    {
        _db = db;
        _jobEnqueuer = jobEnqueuer;
        _cancellations = cancellations;
    }

    public async Task<ApiResponse> Handle(RegenerateChapterMindmapCommand request, CancellationToken ct)
    {
        var chapter = await _db.VideoChapters
            .Include(c => c.LessonVideo)
            .FirstOrDefaultAsync(c => c.Id == request.ChapterId, ct);

        if (chapter == null)
            return ApiResponse.Fail("Chapter not found.");

        var packageContext = await _db.LessonVideos
            .Where(v => v.Id == chapter.LessonVideoId)
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

        var visualStyles = MindmapStyleOptions.ValidVisualStyles(request.VisualStyles);
        var teacherStyles = MindmapStyleOptions.ValidTeacherStyles(request.TeacherStyles);

        var generationRunId = Guid.NewGuid();
        var videoLockRows = await _db.LessonVideos
            .Where(video => video.Id == chapter.LessonVideoId &&
                !video.IsProcessingAI &&
                !video.IsProcessingMindmaps)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(video => video.IsProcessingMindmaps, true)
                .SetProperty(video => video.CurrentMindmapGenerationRunId, generationRunId), ct);

        if (videoLockRows == 0)
            return ApiResponse.Fail("Video is already processing an AI task.");

        try
        {
            var lockRows = await _db.VideoChapters
                .Where(c => c.Id == request.ChapterId && !c.IsRegeneratingMindmap)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.IsRegeneratingMindmap, true)
                    .SetProperty(c => c.CurrentMindmapGenerationRunId, generationRunId), ct);

            if (lockRows == 0)
            {
                await ReleaseVideoLockAsync(chapter.LessonVideoId, generationRunId, ct);
                return ApiResponse.Fail("Chapter mindmap regeneration is already running.");
            }

            await _cancellations.ClearMindmapCancellationAsync(chapter.LessonVideoId);

            await _jobEnqueuer.EnqueueJobAsync("ai-mindmaps-queue", "regenerate-single-mindmap", new
            {
                chapterId = chapter.Id,
                lessonVideoId = chapter.LessonVideoId,
                teacherPhotoUrls,
                visualStyles,
                teacherStyles,
                chapter = new
                {
                    title = chapter.Title,
                    summaryText = chapter.SummaryText,
                    order = chapter.Order
                },
                outputLanguage = AiOutputLanguageContract.ToWorkerCode(packageContext.AiOutputLanguage),
                generationRunId
            });
        }
        catch
        {
            try
            {
                await _db.VideoChapters
                    .Where(c => c.Id == request.ChapterId && c.CurrentMindmapGenerationRunId == generationRunId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(c => c.IsRegeneratingMindmap, false)
                            .SetProperty(c => c.CurrentMindmapGenerationRunId, (Guid?)null),
                        CancellationToken.None);
            }
            finally
            {
                await ReleaseVideoLockAsync(chapter.LessonVideoId, generationRunId, CancellationToken.None);
            }
            throw;
        }

        return ApiResponse.Ok("Mindmap regeneration queued successfully.");
    }

    private async Task ReleaseVideoLockAsync(Guid videoId, Guid generationRunId, CancellationToken ct)
    {
        await _db.LessonVideos
            .Where(video => video.Id == videoId &&
                video.CurrentMindmapGenerationRunId == generationRunId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(video => video.IsProcessingMindmaps, false)
                .SetProperty(video => video.CurrentMindmapGenerationRunId, (Guid?)null), ct);
    }
}
