using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record TrackWatchProgressCommand(Guid LessonVideoId, Guid UserId, double SecondsWatched, bool RegisterView = false) : IRequest<ApiResponse<WatchProgressDto>>;

public record WatchProgressDto(int CurrentCount, int MaxCount, bool IsLocked, bool ViewRegistered);

public class TrackWatchProgressCommandHandler : IRequestHandler<TrackWatchProgressCommand, ApiResponse<WatchProgressDto>>
{
    private readonly IAppDbContext _db;

    public TrackWatchProgressCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<WatchProgressDto>> Handle(TrackWatchProgressCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);
        if (video == null)
            return ApiResponse<WatchProgressDto>.Fail("Video not found");

        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        if (watchEvent == null)
        {
            watchEvent = new VideoWatchEvent
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                LessonVideoId = request.LessonVideoId,
                WatchCount = 0,
                TimeWatchedInSeconds = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsLocked = false
            };
            _db.VideoWatchEvents.Add(watchEvent);
        }

        bool viewRegistered = false;
        
        // Register the view when frontend tells us they reached the threshold in this session
        if (request.RegisterView)
        {
            watchEvent.WatchCount++;
            viewRegistered = true;

            if (watchEvent.WatchCount >= video.MaxWatchCount)
            {
                watchEvent.IsLocked = true;
            }
        }
        
        // Accumulate exactly 10 seconds since we only send ping every 10 actual seconds
        if (!request.RegisterView || request.SecondsWatched > 0) 
        {
            watchEvent.TimeWatchedInSeconds += 10;
        }
        watchEvent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ApiResponse<WatchProgressDto>.Ok(new WatchProgressDto(
            watchEvent.WatchCount,
            video.MaxWatchCount,
            watchEvent.IsLocked,
            viewRegistered
        ));
    }
}
