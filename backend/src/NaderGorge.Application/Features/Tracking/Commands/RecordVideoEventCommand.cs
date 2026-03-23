using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Tracking.Commands;

public record RecordVideoEventCommand(Guid UserId, Guid LessonVideoId, int WatchedSeconds) : IRequest<ApiResponse<VideoTrackingContext>>;

public record VideoTrackingContext(int MaxWatchCount, int WatchCount, bool IsLocked, int RemainingSecondsForNextWatch);

public class RecordVideoEventCommandHandler : IRequestHandler<RecordVideoEventCommand, ApiResponse<VideoTrackingContext>>
{
    private readonly IAppDbContext _db;

    public RecordVideoEventCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<VideoTrackingContext>> Handle(RecordVideoEventCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);
        if (video == null)
            return ApiResponse<VideoTrackingContext>.Fail("Video not found");

        var trackEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        if (trackEvent == null)
            return await CreateNewTrackingEvent(request, video, ct);

        return await UpdateTrackingEvent(request, video, trackEvent, ct);
    }

    private async Task<ApiResponse<VideoTrackingContext>> CreateNewTrackingEvent(RecordVideoEventCommand request, LessonVideo video, CancellationToken ct)
    {
        var newEvent = new VideoWatchEvent
        {
            UserId = request.UserId,
            LessonVideoId = request.LessonVideoId,
            TimeWatchedInSeconds = request.WatchedSeconds,
            WatchCount = 1,
            IsLocked = false
        };

        _db.VideoWatchEvents.Add(newEvent);
        await _db.SaveChangesAsync(ct);

        var ctx = new VideoTrackingContext(video.MaxWatchCount, newEvent.WatchCount, newEvent.IsLocked, 0); // threshold calc ignored for 1st partial 
        return ApiResponse<VideoTrackingContext>.Ok(ctx);
    }

    private async Task<ApiResponse<VideoTrackingContext>> UpdateTrackingEvent(RecordVideoEventCommand request, LessonVideo video, VideoWatchEvent trackEvent, CancellationToken ct)
    {
        if (trackEvent.IsLocked)
            return ApiResponse<VideoTrackingContext>.Fail("Maximum watch limit reached. Video is locked.");

        // Here we increment the watched time
        trackEvent.TimeWatchedInSeconds += request.WatchedSeconds;

        // Roughly consider a 'watch' as 80% of video or predefined chunk length
        // To simplify, if TimeWatched exceeds N seconds (e.g. 3600), increment WatchCount.
        // For MVP: Increment watch count every 60 min of total watch time (or whatever metric). Let's use 30 minutes for demo.
        int secondsThreshold = 1800; // 30 minutes
        if (trackEvent.TimeWatchedInSeconds >= (trackEvent.WatchCount * secondsThreshold))
        {
            trackEvent.WatchCount++;
            
            if (trackEvent.WatchCount >= video.MaxWatchCount)
            {
                trackEvent.IsLocked = true;
            }
        }

        await _db.SaveChangesAsync(ct);

        int remainingSeconds = (trackEvent.WatchCount * secondsThreshold) - trackEvent.TimeWatchedInSeconds;

        var ctx = new VideoTrackingContext(video.MaxWatchCount, trackEvent.WatchCount, trackEvent.IsLocked, remainingSeconds > 0 ? remainingSeconds : 0);
        return ApiResponse<VideoTrackingContext>.Ok(ctx, "Progress tracked");
    }
}
