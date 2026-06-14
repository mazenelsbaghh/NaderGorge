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
            var lockedMaxLimit = trackEvent.CustomMaxWatchCount ?? video.MaxWatchCount;
            var lockedContext = new VideoTrackingContext(
                lockedMaxLimit,
                lockedMaxLimit > 0 ? Math.Min(trackEvent.WatchCount, lockedMaxLimit) : trackEvent.WatchCount,
                true,
                0);
            return ApiResponse<VideoTrackingContext>.Fail(
                "Maximum watch limit reached. Video is locked.",
                new List<string> { "WATCH_LIMIT_REACHED" },
                lockedContext);
        }

        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var thresholdPercentage = settings.VideoWatchThresholdPercentage;
        var secondsThreshold = VideoWatchProgressCalculator.ResolveThresholdSeconds(request.TotalDurationSeconds, thresholdPercentage);

        // If TimeWatchedInSeconds is negative, it indicates a reset signal (e.g. from an approved extra watch request)
        if (trackEvent.TimeWatchedInSeconds < 0)
        {
            trackEvent.TimeWatchedInSeconds = trackEvent.WatchCount * secondsThreshold;
        }

        var acceptedSeconds = VideoWatchProgressCalculator.ResolveAcceptedSeconds(
            request.WatchedSeconds,
            now,
            trackEvent,
            isNewTrackingEvent);

        int maxLimit = trackEvent.CustomMaxWatchCount ?? video.MaxWatchCount;
        var progressResult = VideoWatchProgressCalculator.ApplyProgress(
            trackEvent,
            acceptedSeconds,
            secondsThreshold,
            maxLimit);

        trackEvent.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var ctx = new VideoTrackingContext(
            trackEvent.CustomMaxWatchCount ?? video.MaxWatchCount,
            maxLimit > 0 ? Math.Min(trackEvent.WatchCount, maxLimit) : trackEvent.WatchCount,
            trackEvent.IsLocked,
            progressResult.RemainingSecondsForNextWatch);
        return ApiResponse<VideoTrackingContext>.Ok(ctx, "Progress tracked");
    }
}
