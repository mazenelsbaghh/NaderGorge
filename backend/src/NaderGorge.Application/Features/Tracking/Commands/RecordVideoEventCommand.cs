using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Tracking.Commands;

public record RecordVideoEventCommand(Guid UserId, Guid LessonVideoId, int WatchedSeconds, int TotalDurationSeconds = 0) : IRequest<ApiResponse<VideoTrackingContext>>;

public record VideoTrackingContext(int MaxWatchCount, int WatchCount, bool IsLocked, int RemainingSecondsForNextWatch);

public class RecordVideoEventCommandHandler : IRequestHandler<RecordVideoEventCommand, ApiResponse<VideoTrackingContext>>
{
    private readonly IAppDbContext _db;
    private readonly ICachedPlatformSettingsReader _cachedPlatformSettingsReader;

    public RecordVideoEventCommandHandler(IAppDbContext db, ICachedPlatformSettingsReader cachedPlatformSettingsReader)
    {
        _db = db;
        _cachedPlatformSettingsReader = cachedPlatformSettingsReader;
    }

    public async Task<ApiResponse<VideoTrackingContext>> Handle(RecordVideoEventCommand request, CancellationToken ct)
    {
        if (request.TotalDurationSeconds <= 0)
            return ApiResponse<VideoTrackingContext>.Fail("Duration required", new List<string> { "DURATION_REQUIRED" });

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
        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var thresholdPercentage = settings.VideoWatchThresholdPercentage;
        var secondsThreshold = ResolveThresholdSeconds(request.TotalDurationSeconds, thresholdPercentage);

        var newEvent = new VideoWatchEvent
        {
            UserId = request.UserId,
            LessonVideoId = request.LessonVideoId,
            TimeWatchedInSeconds = request.WatchedSeconds,
            WatchCount = 0,
            IsLocked = false
        };

        if (newEvent.TimeWatchedInSeconds >= secondsThreshold)
        {
            newEvent.WatchCount = 1;
            if (video.MaxWatchCount > 0 && newEvent.WatchCount > video.MaxWatchCount)
            {
                newEvent.IsLocked = true;
            }
        }

        _db.VideoWatchEvents.Add(newEvent);
        await _db.SaveChangesAsync(ct);

        var remainingSeconds = Math.Max(0, secondsThreshold - newEvent.TimeWatchedInSeconds);
        var ctx = new VideoTrackingContext(video.MaxWatchCount, newEvent.WatchCount, newEvent.IsLocked, remainingSeconds);
        return ApiResponse<VideoTrackingContext>.Ok(ctx);
    }

    private async Task<ApiResponse<VideoTrackingContext>> UpdateTrackingEvent(RecordVideoEventCommand request, LessonVideo video, VideoWatchEvent trackEvent, CancellationToken ct)
    {
        if (trackEvent.IsLocked)
            return ApiResponse<VideoTrackingContext>.Fail("Maximum watch limit reached. Video is locked.");

        // Here we increment the watched time
        trackEvent.TimeWatchedInSeconds += request.WatchedSeconds;

        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var thresholdPercentage = settings.VideoWatchThresholdPercentage;
        var secondsThreshold = ResolveThresholdSeconds(request.TotalDurationSeconds, thresholdPercentage);

        var shouldIncrement = trackEvent.TimeWatchedInSeconds >= (trackEvent.WatchCount + 1) * secondsThreshold;
        if (shouldIncrement)
        {
            trackEvent.WatchCount++;
            
            if (video.MaxWatchCount > 0 && trackEvent.WatchCount > video.MaxWatchCount)
            {
                trackEvent.IsLocked = true;
            }
        }

        await _db.SaveChangesAsync(ct);

        int remainingSeconds = ((trackEvent.WatchCount + 1) * secondsThreshold) - trackEvent.TimeWatchedInSeconds;

        var ctx = new VideoTrackingContext(video.MaxWatchCount, trackEvent.WatchCount, trackEvent.IsLocked, remainingSeconds > 0 ? remainingSeconds : 0);
        return ApiResponse<VideoTrackingContext>.Ok(ctx, "Progress tracked");
    }

    private static int ResolveThresholdSeconds(int totalDurationSeconds, int thresholdPercentage)
    {
        var threshold = VideoWatchThresholdCalculator.CalculateThresholdSeconds(totalDurationSeconds, thresholdPercentage);
        return Math.Max(1, threshold);
    }
}
