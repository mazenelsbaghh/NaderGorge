using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record SingleMindmapFailedCommand(Guid ChapterId) : IRequest<ApiResponse>;

public class SingleMindmapFailedCommandHandler(IAppDbContext db) : IRequestHandler<SingleMindmapFailedCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(SingleMindmapFailedCommand request, CancellationToken ct)
    {
        var updatedRows = await db.VideoChapters
            .Where(chapter => chapter.Id == request.ChapterId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(chapter => chapter.IsRegeneratingMindmap, false), ct);

        return updatedRows == 0
            ? ApiResponse.Fail("Chapter not found.")
            : ApiResponse.Ok("Chapter mindmap regeneration state cleared.");
    }
}
