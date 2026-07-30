using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIVerificationService
{
    Task<LiveSupportAIVerificationStateDto> StartLookupAsync(
        LiveSupportParticipantIdentity participant,
        Guid conversationId,
        LiveSupportAIVerificationLookupCommandDto request,
        CancellationToken cancellationToken);

    Task<LiveSupportAIVerificationStateDto> SubmitAnswerAsync(
        LiveSupportParticipantIdentity participant,
        Guid conversationId,
        LiveSupportAIVerificationAnswerCommandDto request,
        CancellationToken cancellationToken);
}
