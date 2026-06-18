using MediatR;
using NaderGorge.Application.Common;

namespace NaderGorge.Application.Features.Tracking.Commands;

public record RecordVideoEventCommand(Guid UserId, Guid LessonVideoId, int WatchedSeconds, int TotalDurationSeconds = 0) : IRequest<ApiResponse<VideoTrackingContext>>;

public record VideoTrackingContext(int MaxWatchCount, int WatchCount, bool IsLocked, int RemainingSecondsForNextWatch);

public class RecordVideoEventCommandHandler : IRequestHandler<RecordVideoEventCommand, ApiResponse<VideoTrackingContext>>
{
    public Task<ApiResponse<VideoTrackingContext>> Handle(RecordVideoEventCommand request, CancellationToken ct)
    {
        return Task.FromResult(ApiResponse<VideoTrackingContext>.Fail(
            "A playback session is required to track video progress.",
            new List<string> { "SESSION_REQUIRED" }));
    }
}
