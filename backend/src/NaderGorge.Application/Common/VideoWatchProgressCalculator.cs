using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Common;

public static class VideoWatchProgressCalculator
{
    private const int MaxAcceptedSecondsPerSync = 30;
    private const int TrackingClockGraceSeconds = 5;

    public static int ResolveThresholdSeconds(int totalDurationSeconds, int thresholdPercentage)
    {
        var threshold = VideoWatchThresholdCalculator.CalculateThresholdSeconds(totalDurationSeconds, thresholdPercentage);
        return Math.Max(1, threshold);
    }

    public static int ResolveAcceptedSeconds(double reportedSeconds, DateTime now, VideoWatchEvent watchEvent, bool isNewWatchEvent)
    {
        var sanitizedReportedSeconds = (int)Math.Max(0, Math.Round(reportedSeconds, MidpointRounding.AwayFromZero));
        var maxByElapsedTime = isNewWatchEvent
            ? MaxAcceptedSecondsPerSync
            : Math.Max(0, (int)Math.Ceiling((now - (watchEvent.UpdatedAt ?? watchEvent.CreatedAt)).TotalSeconds) + TrackingClockGraceSeconds);

        return Math.Min(sanitizedReportedSeconds, Math.Min(maxByElapsedTime, MaxAcceptedSecondsPerSync));
    }

    public static VideoWatchProgressCalculationResult ApplyProgress(
        VideoWatchEvent watchEvent,
        int acceptedSeconds,
        int thresholdSeconds,
        int maxWatchCount)
    {
        var previousWatchCount = watchEvent.WatchCount;

        if (acceptedSeconds > 0)
        {
            watchEvent.TimeWatchedInSeconds += acceptedSeconds;
        }

        // If the admin recently increased the maxWatchCount (unlocked the student),
        // their accumulated TimeWatchedInSeconds might be higher than the new max limit threshold.
        // We reset it to the beginning of their current WatchCount so they must watch the full
        // thresholdSeconds of new time to register the next watch.


        if (maxWatchCount > 0 && watchEvent.WatchCount > maxWatchCount)
        {
            watchEvent.WatchCount = maxWatchCount;
        }

        while (watchEvent.TimeWatchedInSeconds >= (watchEvent.WatchCount + 1) * thresholdSeconds
               && (maxWatchCount <= 0 || watchEvent.WatchCount < maxWatchCount))
        {
            watchEvent.WatchCount++;
        }

        if (maxWatchCount > 0 && watchEvent.WatchCount >= maxWatchCount)
        {
            watchEvent.WatchCount = maxWatchCount;
            watchEvent.IsLocked = true;
            if (watchEvent.TimeWatchedInSeconds > maxWatchCount * thresholdSeconds)
            {
                watchEvent.TimeWatchedInSeconds = maxWatchCount * thresholdSeconds;
            }
        }

        var remainingSeconds = maxWatchCount > 0 && watchEvent.WatchCount >= maxWatchCount
            ? 0
            : Math.Max(0, ((watchEvent.WatchCount + 1) * thresholdSeconds) - watchEvent.TimeWatchedInSeconds);

        return new VideoWatchProgressCalculationResult(
            watchEvent.WatchCount > previousWatchCount,
            remainingSeconds);
    }
}

public record VideoWatchProgressCalculationResult(bool ViewRegistered, int RemainingSecondsForNextWatch);
