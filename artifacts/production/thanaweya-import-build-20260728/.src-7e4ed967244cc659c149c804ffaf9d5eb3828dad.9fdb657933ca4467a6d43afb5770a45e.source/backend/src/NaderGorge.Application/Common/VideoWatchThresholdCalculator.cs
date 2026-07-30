namespace NaderGorge.Application.Common;

public static class VideoWatchThresholdCalculator
{
    public static int CalculateThresholdSeconds(int durationSeconds, double percentage)
        => (int)Math.Round(durationSeconds * (percentage / 100.0));
}
