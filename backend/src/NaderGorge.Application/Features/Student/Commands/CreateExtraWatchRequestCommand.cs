using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record CreateExtraWatchRequestCommand(Guid LessonVideoId, Guid UserId) : IRequest<ApiResponse<Guid>>;

public class CreateExtraWatchRequestCommandHandler : IRequestHandler<CreateExtraWatchRequestCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _context;
    private readonly ICachedPlatformSettingsReader _cachedPlatformSettingsReader;

    public CreateExtraWatchRequestCommandHandler(IAppDbContext context, ICachedPlatformSettingsReader cachedPlatformSettingsReader)
    {
        _context = context;
        _cachedPlatformSettingsReader = cachedPlatformSettingsReader;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateExtraWatchRequestCommand request, CancellationToken cancellationToken)
    {
        // Ensure video exists
        var video = await _context.LessonVideos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, cancellationToken);
            
        if (video == null)
            return ApiResponse<Guid>.Fail("Video not found", new List<string> { "VIDEO_NOT_FOUND" });

        var existingPending = await _context.ExtraWatchRequests
            .AnyAsync(r => r.LessonVideoId == request.LessonVideoId && r.UserId == request.UserId && r.Status == RequestStatus.Pending, cancellationToken);

        if (existingPending)
            return ApiResponse<Guid>.Fail("A pending request already exists.", new List<string> { "REQUEST_EXISTS" });

        var settings = await _cachedPlatformSettingsReader.GetAsync(cancellationToken);
        var totalRequestsForVideo = await _context.ExtraWatchRequests
            .CountAsync(
                requestItem => requestItem.LessonVideoId == request.LessonVideoId && requestItem.UserId == request.UserId,
                cancellationToken);

        if (totalRequestsForVideo >= settings.MaxExtraWatchRequestsPerVideo)
        {
            return ApiResponse<Guid>.Fail(
                "You have reached the maximum number of extra watch requests for this video.",
                new List<string> { "REQUEST_LIMIT_REACHED" });
        }

        var watchRequest = new ExtraWatchRequest
        {
            UserId = request.UserId,
            LessonVideoId = request.LessonVideoId,
            Status = RequestStatus.Pending
        };

        _context.ExtraWatchRequests.Add(watchRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<Guid>.Ok(watchRequest.Id);
    }
}
