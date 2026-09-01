namespace NaderGorge.Application.Interfaces;

public interface IVideoPlaybackConcurrency
{
    Task AcquireAsync(Guid userId, Guid lessonVideoId, CancellationToken cancellationToken);
}
