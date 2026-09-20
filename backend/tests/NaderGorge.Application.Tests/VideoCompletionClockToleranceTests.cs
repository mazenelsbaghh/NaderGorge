using NaderGorge.Application.Common;

namespace NaderGorge.Application.Tests;

public sealed class VideoCompletionClockToleranceTests
{
    // 2026-09-15: an acknowledged final fractional second must not strand a multipart lesson.
    [Theory]
    [InlineData(100, 99.999, true)]
    [InlineData(100, 98.9, false)]
    [InlineData(3600, 3598, true)]
    [InlineData(3600, 3597.9, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 100, false)]
    public void Completion_BoundsClockTolerancePerPart(int duration, double watched, bool completed)
    {
        var part = new StudentVideoProgress(Guid.NewGuid(), Guid.NewGuid(), duration, (decimal)watched);
        Assert.Equal(completed, part.IsCompleted);
        if (completed) Assert.Equal(100, StudentWatchProgressReader.CalculatePercent([part]));
    }

    [Fact]
    public void MultipartCompletion_DoesNotLetReplaysCoverAnUnwatchedPart()
    {
        var lesson = Guid.NewGuid();
        StudentVideoProgress[] parts = [new(Guid.NewGuid(), lesson, 100, 200), new(Guid.NewGuid(), lesson, 100, 0)];
        Assert.Equal(50, StudentWatchProgressReader.CalculatePercent(parts));
        Assert.False(parts.All(part => part.IsCompleted));
    }
}
