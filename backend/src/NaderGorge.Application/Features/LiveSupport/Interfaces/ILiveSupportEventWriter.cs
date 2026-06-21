using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public sealed record LiveSupportEventWriteRequest(Guid ConversationId, LiveSupportEventType Type, Guid? ActorUserId = null, Guid? ActorGuestSessionId = null, Guid? RelatedEntityId = null, string? SafeMetadataJson = null, IReadOnlyList<string>? TargetGroups = null);

public interface ILiveSupportEventWriter
{
    Task<long> AppendAsync(LiveSupportEventWriteRequest request, CancellationToken ct);
}
