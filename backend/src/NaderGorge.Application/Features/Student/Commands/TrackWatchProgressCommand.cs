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

        var trackedSecondsDelta = (int)Math.Max(0, Math.Round(request.SecondsWatched, MidpointRounding.AwayFromZero));
        if (trackedSecondsDelta > 0)
        {
            watchEvent.TimeWatchedInSeconds += trackedSecondsDelta;
        }

        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var thresholdPercentage = settings.VideoWatchThresholdPercentage;

        var thresholdSeconds = Math.Max(
            1,
            VideoWatchThresholdCalculator.CalculateThresholdSeconds(request.TotalDurationSeconds, thresholdPercentage)
        );

        var previousWatchCount = watchEvent.WatchCount;

        var shouldIncrement = watchEvent.TimeWatchedInSeconds >= (watchEvent.WatchCount + 1) * thresholdSeconds;
        if (shouldIncrement)
        {
            watchEvent.WatchCount++;
        }

        var viewRegistered = watchEvent.WatchCount > previousWatchCount;

        if (video.MaxWatchCount > 0 && watchEvent.WatchCount > video.MaxWatchCount)
        {
            watchEvent.IsLocked = true;
        }

        watchEvent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ApiResponse<WatchProgressDto>.Ok(new WatchProgressDto(
            watchEvent.WatchCount,
            video.MaxWatchCount,
            watchEvent.IsLocked,
            viewRegistered,
            watchEvent.TimeWatchedInSeconds,
            thresholdSeconds
        ));
    }
}
