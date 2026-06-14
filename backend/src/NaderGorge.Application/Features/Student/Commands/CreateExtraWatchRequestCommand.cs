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
    private readonly ICachedPlatformSettingsReader _settingsReader;

    public CreateExtraWatchRequestCommandHandler(IAppDbContext context, ICachedPlatformSettingsReader settingsReader)
    {
        _context = context;
        _settingsReader = settingsReader;
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

        // Enforce maximum extra watch requests per video
        var settings = await _settingsReader.GetAsync(cancellationToken);
        var maxRequests = settings.MaxExtraWatchRequestsPerVideo;

        var totalRequestsCount = await _context.ExtraWatchRequests
            .CountAsync(r => r.LessonVideoId == request.LessonVideoId && r.UserId == request.UserId, cancellationToken);

        if (totalRequestsCount >= maxRequests)
            return ApiResponse<Guid>.Fail("Extra watch request limit reached.", new List<string> { "REQUEST_LIMIT_REACHED" });

        var watchRequest = new ExtraWatchRequest
        {
            UserId = request.UserId,
            LessonVideoId = request.LessonVideoId,
            Status = RequestStatus.Pending
        };

        _context.ExtraWatchRequests.Add(watchRequest);

        var outboxEvent = new OutboxEvent
        {
            Type = "ExtraWatchRequestCreated",
            TargetGroup = "Role_Admin",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                requestId = watchRequest.Id,
                videoId = request.LessonVideoId,
                studentId = request.UserId
            })
        };
        _context.OutboxEvents.Add(outboxEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<Guid>.Ok(watchRequest.Id);
    }
}
