using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record TrackWatchProgressCommand(
    Guid LessonVideoId,
    Guid UserId,
    double SecondsWatched,
    int TotalDurationSeconds = 0,
    bool RegisterView = false
) : IRequest<ApiResponse<WatchProgressDto>>;

public record WatchProgressDto(
    int CurrentCount,
    int MaxCount,
    bool IsLocked,
    bool ViewRegistered,
    int TotalTrackedSeconds,
    int ThresholdSeconds
);

public class TrackWatchProgressCommandHandler : IRequestHandler<TrackWatchProgressCommand, ApiResponse<WatchProgressDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICachedPlatformSettingsReader _cachedPlatformSettingsReader;

    public TrackWatchProgressCommandHandler(IAppDbContext db, ICachedPlatformSettingsReader cachedPlatformSettingsReader)
    {
        _db = db;
        _cachedPlatformSettingsReader = cachedPlatformSettingsReader;
    }

    public async Task<ApiResponse<WatchProgressDto>> Handle(TrackWatchProgressCommand request, CancellationToken ct)
    {
        if (request.TotalDurationSeconds <= 0)
            return ApiResponse<WatchProgressDto>.Fail("Duration required", new List<string> { "DURATION_REQUIRED" });

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);
        if (video == null)
            return ApiResponse<WatchProgressDto>.Fail("Video not found");

        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        var now = DateTime.UtcNow;
        var isNewWatchEvent = watchEvent == null;
        if (watchEvent == null)
        {
            watchEvent = new VideoWatchEvent
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                LessonVideoId = request.LessonVideoId,
                WatchCount = 0,
                TimeWatchedInSeconds = 0,
                CreatedAt = now,
                UpdatedAt = now,
                IsLocked = false
            };
            _db.VideoWatchEvents.Add(watchEvent);
        }

        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var thresholdPercentage = settings.VideoWatchThresholdPercentage;

        var thresholdSeconds = VideoWatchProgressCalculator.ResolveThresholdSeconds(request.TotalDurationSeconds, thresholdPercentage);

        int maxLimit = watchEvent.CustomMaxWatchCount ?? video.MaxWatchCount;

        // If TimeWatchedInSeconds is negative, it indicates a reset signal (e.g. from an approved extra watch request)
        if (watchEvent.TimeWatchedInSeconds < 0)
        {
            watchEvent.TimeWatchedInSeconds = watchEvent.WatchCount * thresholdSeconds;
        }

        bool isLocked = maxLimit > 0 && watchEvent.WatchCount >= maxLimit;
        if (isLocked)
        {
            if (!watchEvent.IsLocked)
            {
                watchEvent.IsLocked = true;
                await _db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return ApiResponse<WatchProgressDto>.Ok(new WatchProgressDto(
                maxLimit > 0 ? Math.Min(watchEvent.WatchCount, maxLimit) : watchEvent.WatchCount,
                maxLimit,
                true,
                false,
                Math.Max(0, watchEvent.TimeWatchedInSeconds),
                thresholdSeconds
            ));
        }
        else if (watchEvent.IsLocked)
        {
            watchEvent.IsLocked = false;
            await _db.SaveChangesAsync(ct);
        }


        var trackedSecondsDelta = VideoWatchProgressCalculator.ResolveAcceptedSeconds(
            request.SecondsWatched,
            now,
            watchEvent,
            isNewWatchEvent);

        var progressResult = VideoWatchProgressCalculator.ApplyProgress(
            watchEvent,
            trackedSecondsDelta,
            thresholdSeconds,
            maxLimit);

        watchEvent.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ApiResponse<WatchProgressDto>.Ok(new WatchProgressDto(
            maxLimit > 0 ? Math.Min(watchEvent.WatchCount, maxLimit) : watchEvent.WatchCount,
            maxLimit,
            watchEvent.IsLocked,
            progressResult.ViewRegistered,
            Math.Max(0, watchEvent.TimeWatchedInSeconds),
            thresholdSeconds
        ));
    }
}
