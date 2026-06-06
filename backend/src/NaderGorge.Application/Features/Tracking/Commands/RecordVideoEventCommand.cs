using System.Data;
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

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);
        if (video == null)
            return ApiResponse<VideoTrackingContext>.Fail("Video not found");

        var trackEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        var now = DateTime.UtcNow;
        var isNewTrackingEvent = trackEvent == null;
        if (trackEvent == null)
        {
            trackEvent = new VideoWatchEvent
            {
                UserId = request.UserId,
                LessonVideoId = request.LessonVideoId,
                TimeWatchedInSeconds = 0,
                WatchCount = 0,
                IsLocked = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.VideoWatchEvents.Add(trackEvent);
        }

        if (trackEvent.IsLocked)
        {
            await transaction.CommitAsync(ct);
            return ApiResponse<VideoTrackingContext>.Fail("Maximum watch limit reached. Video is locked.");
        }

        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var thresholdPercentage = settings.VideoWatchThresholdPercentage;
        var secondsThreshold = ResolveThresholdSeconds(request.TotalDurationSeconds, thresholdPercentage);

        var acceptedSeconds = ResolveAcceptedSeconds(request.WatchedSeconds, now, trackEvent, isNewTrackingEvent);
        if (acceptedSeconds > 0)
        {
            trackEvent.TimeWatchedInSeconds += acceptedSeconds;
        }

        while (trackEvent.TimeWatchedInSeconds >= (trackEvent.WatchCount + 1) * secondsThreshold)
        {
            trackEvent.WatchCount++;
        }

        int maxLimit = trackEvent.CustomMaxWatchCount ?? video.MaxWatchCount;
        if (maxLimit > 0 && trackEvent.WatchCount >= maxLimit)
        {
            trackEvent.IsLocked = true;
        }

        trackEvent.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        int remainingSeconds = ((trackEvent.WatchCount + 1) * secondsThreshold) - trackEvent.TimeWatchedInSeconds;

        var ctx = new VideoTrackingContext(trackEvent.CustomMaxWatchCount ?? video.MaxWatchCount, trackEvent.WatchCount, trackEvent.IsLocked, remainingSeconds > 0 ? remainingSeconds : 0);
        return ApiResponse<VideoTrackingContext>.Ok(ctx, "Progress tracked");
    }

    private static int ResolveThresholdSeconds(int totalDurationSeconds, int thresholdPercentage)
    {
        var threshold = VideoWatchThresholdCalculator.CalculateThresholdSeconds(totalDurationSeconds, thresholdPercentage);
        return Math.Max(1, threshold);
    }

    private static int ResolveAcceptedSeconds(int reportedSeconds, DateTime now, VideoWatchEvent trackEvent, bool isNewTrackingEvent)
    {
        var sanitizedReportedSeconds = Math.Max(0, reportedSeconds);
        var maxByElapsedTime = isNewTrackingEvent
            ? 30
            : Math.Max(0, (int)Math.Ceiling((now - (trackEvent.UpdatedAt ?? trackEvent.CreatedAt)).TotalSeconds) + 5);

        return Math.Min(sanitizedReportedSeconds, Math.Min(maxByElapsedTime, 30));
    }
}
