using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIHandoffService
{
    Task<string> HandoffAsync(
        Guid conversationId,
        LiveSupportParticipantIdentity? participant,
        Guid? actorUserId,
        string reasonCode,
        string safeSummary,
        bool forced,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
