namespace NaderGorge.Application.Common.HR;

public interface IHrRequestContext
{
    Guid? ActorUserId { get; }
    string CorrelationId { get; }
    string? IpAddress => null;
    string RequestId => CorrelationId;

    Guid RequireActorUserId() => ActorUserId
        ?? throw new UnauthorizedAccessException("HR mutation requires an authenticated actor.");
}
