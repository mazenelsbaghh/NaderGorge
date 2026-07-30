using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record SingleMindmapCompletedCommand(Guid ChapterId, string ImageUrl) : IRequest<ApiResponse>;

public class SingleMindmapCompletedCommandHandler : IRequestHandler<SingleMindmapCompletedCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public SingleMindmapCompletedCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(SingleMindmapCompletedCommand request, CancellationToken ct)
    {
        var chapter = await _db.VideoChapters
            .FirstOrDefaultAsync(c => c.Id == request.ChapterId, ct);

        if (chapter == null)
            return ApiResponse.Fail("Chapter not found.");

        chapter.MindmapImageUrl = request.ImageUrl;
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok("Single mindmap image updated successfully.");
    }
}
