namespace NaderGorge.Application.Features.Student;

public static class VideoPlaybackSessionPolicy
{
    public static readonly TimeSpan MinimumLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(2);
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan PlaybackMargin = TimeSpan.FromMinutes(30);

    public static TimeSpan ResolveLifetime(int? durationSeconds)
    {
        if (durationSeconds is null or <= 0)
            return DefaultLifetime;

        var requestedSeconds = durationSeconds.Value + PlaybackMargin.TotalSeconds;
        var boundedSeconds = Math.Clamp(
            requestedSeconds,
            MinimumLifetime.TotalSeconds,
            MaximumLifetime.TotalSeconds);
        return TimeSpan.FromSeconds(boundedSeconds);
    }
}
