using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public interface ILiveSupportHumanConversationFactory
{
    Task<LiveSupportConversationDto> CreateHumanOnlyAsync(
        LiveSupportParticipantIdentity participant,
        string? subject,
        Guid? previousConversationId,
        CancellationToken ct);
}
