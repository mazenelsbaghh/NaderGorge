using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIRegistrationService
{
    Task<Guid> RegisterAndLinkAsync(
        LiveSupportParticipantIdentity participant,
        Guid conversationId,
        LiveSupportAISecureRegistrationDto request,
        CancellationToken cancellationToken);
}
