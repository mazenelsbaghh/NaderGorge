using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record SingleMindmapFailedCommand(Guid ChapterId, Guid? GenerationRunId = null) : IRequest<ApiResponse>;

public class SingleMindmapFailedCommandHandler(IAppDbContext db) : IRequestHandler<SingleMindmapFailedCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(SingleMindmapFailedCommand request, CancellationToken ct)
    {
        var chapter = await db.VideoChapters
            .Include(candidate => candidate.LessonVideo)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.ChapterId, ct);
        if (chapter == null)
            return ApiResponse.Fail("Chapter not found.");

        var isLegacyRun = !request.GenerationRunId.HasValue &&
            !chapter.CurrentMindmapGenerationRunId.HasValue &&
            chapter.IsRegeneratingMindmap;
        var isCurrentFencedRun = request.GenerationRunId.HasValue &&
            AiGenerationRunContract.IsCurrent(
                chapter.CurrentMindmapGenerationRunId,
                request.GenerationRunId,
                chapter.IsRegeneratingMindmap) &&
            AiGenerationRunContract.IsCurrent(
                chapter.LessonVideo.CurrentMindmapGenerationRunId,
                request.GenerationRunId,
                chapter.LessonVideo.IsProcessingMindmaps);
        if (!isLegacyRun && !isCurrentFencedRun)
            return ApiResponse.Ok("Stale single mindmap failure callback ignored.");

        chapter.IsRegeneratingMindmap = false;
        chapter.CurrentMindmapGenerationRunId = null;
        if (isCurrentFencedRun)
        {
            chapter.LessonVideo.IsProcessingMindmaps = false;
            chapter.LessonVideo.CurrentMindmapGenerationRunId = null;
        }
        await db.SaveChangesAsync(ct);

        return ApiResponse.Ok("Chapter mindmap regeneration state cleared.");
    }
}
