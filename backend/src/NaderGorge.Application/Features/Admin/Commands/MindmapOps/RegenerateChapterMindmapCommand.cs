using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands.MindmapOps;

public record RegenerateChapterMindmapCommand(Guid ChapterId) : IRequest<ApiResponse>;

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

        var teacherPhotoUrl = await _db.TeacherPhotos
            .Where(tp => tp.IsActive)
            .OrderByDescending(tp => tp.UploadedAt)
            .Select(tp => tp.FileUrl)
            .FirstOrDefaultAsync(ct);

        // Enqueue a single-chapter mindmap job (worker handles chapterId payload)
        await _jobEnqueuer.EnqueueJobAsync("ai-mindmaps-queue", "regenerate-single-mindmap", new
        {
            chapterId = chapter.Id,
            lessonVideoId = chapter.LessonVideoId,
            teacherPhotoUrl = teacherPhotoUrl,
            chapter = new
            {
                title = chapter.Title,
                summaryText = chapter.SummaryText,
                order = chapter.Order
            }
        });

        return ApiResponse.Ok("Mindmap regeneration queued successfully.");
    }
}
