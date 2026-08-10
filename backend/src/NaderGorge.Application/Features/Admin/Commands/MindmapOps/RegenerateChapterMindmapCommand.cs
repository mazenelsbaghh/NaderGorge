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

    public RegenerateChapterMindmapCommandHandler(IAppDbContext db, IJobEnqueuer jobEnqueuer)
    {
        _db = db;
        _jobEnqueuer = jobEnqueuer;
    }

    public async Task<ApiResponse> Handle(RegenerateChapterMindmapCommand request, CancellationToken ct)
    {
        var chapter = await _db.VideoChapters
            .Include(c => c.LessonVideo)
            .FirstOrDefaultAsync(c => c.Id == request.ChapterId, ct);

        if (chapter == null)
            return ApiResponse.Fail("Chapter not found.");

        var teacherUserId = await _db.LessonVideos
            .Where(v => v.Id == chapter.LessonVideoId)
            .Select(v => (Guid?)v.Lesson.ContentSection.Term.Package.Teacher.UserId)
            .FirstOrDefaultAsync(ct);

        var teacherPhotoUrls = new List<string>();
        if (teacherUserId != null)
        {
            teacherPhotoUrls = await _db.TeacherPhotos
                .Where(tp => tp.TeacherId == teacherUserId.Value && tp.IsActive)
                .OrderByDescending(tp => tp.UploadedAt)
                .Take(1)
                .Select(tp => tp.FileUrl)
                .ToListAsync(ct);
        }

        if (teacherPhotoUrls.Count == 0)
            return ApiResponse.Fail("لا توجد صورة نشطة للمدرس. ارفع صورة واضحة وفعّلها قبل توليد الصور.");

        var visualStyles = MindmapStyleOptions.ValidVisualStyles(request.VisualStyles);
        var teacherStyles = MindmapStyleOptions.ValidTeacherStyles(request.TeacherStyles);

        var lockRows = await _db.VideoChapters
            .Where(c => c.Id == request.ChapterId && !c.IsRegeneratingMindmap)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsRegeneratingMindmap, true), ct);

        if (lockRows == 0)
            return ApiResponse.Fail("Chapter mindmap regeneration is already running.");

        try
        {
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
                }
            });
        }
        catch
        {
            await _db.VideoChapters
                .Where(c => c.Id == request.ChapterId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(c => c.IsRegeneratingMindmap, false),
                    CancellationToken.None);
            throw;
        }

        return ApiResponse.Ok("Mindmap regeneration queued successfully.");
    }
}
