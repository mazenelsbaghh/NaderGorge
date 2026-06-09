using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record MindmapDto(string Title, string ImageUrl);

public record MindmapsCompletedCommand(Guid VideoId, List<MindmapDto> Mindmaps) : IRequest<ApiResponse>;

public class MindmapsCompletedCommandHandler : IRequestHandler<MindmapsCompletedCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public MindmapsCompletedCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(MindmapsCompletedCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos
            .Include(v => v.VideoChapters)
            .FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);

        if (video == null) return ApiResponse.Fail("Video not found");

        foreach (var mindmapData in request.Mindmaps)
        {
            var chapter = video.VideoChapters.FirstOrDefault(c => c.Title == mindmapData.Title);
            if (chapter != null)
            {
                chapter.MindmapImageUrl = mindmapData.ImageUrl;
            }
        }

        video.IsProcessingMindmaps = false;

        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok("Mindmaps updated successfully");
    }
}
