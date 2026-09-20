using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public class LessonDurationRecoveryTests
{
    [Theory]
    [InlineData(120)]
    [InlineData(null)]
    public async Task MissingDurationUsesTrustedProviderAndDoesNotBorrowAnotherStudentsWatch(int? providerDuration)
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var video = new LessonVideo { LessonId = Guid.NewGuid(), Provider = "bunny", ProviderVideoId = "video", BunnyStreamLibraryId = Guid.NewGuid() };
        db.LessonVideos.Add(video);
        db.VideoWatchEvents.Add(new VideoWatchEvent { UserId = Guid.NewGuid(), LessonVideoId = video.Id, LearningWatchedSeconds = 120, LearningDurationSeconds = 120 });
        await db.SaveChangesAsync();
        var progress = await StudentWatchProgressReader.ReadAsync(new(db, studentId, [video.LessonId]), [video.Id], CancellationToken.None, new ProviderDuration(providerDuration));
        var result = Assert.Single(progress);
        Assert.Equal(providerDuration, result.DurationSeconds);
        Assert.Equal(0, result.WatchedSeconds);
        Assert.False(result.IsCompleted);
    }

    private sealed class ProviderDuration(int? seconds) : IBunnyVideoDurationResolver
    {
        public Task<int?> ResolveAsync(Guid libraryId, string videoGuid, CancellationToken cancellationToken) => Task.FromResult(seconds);
    }
}
