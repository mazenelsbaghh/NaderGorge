using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAITurnOrchestrator
{
    Task QueueForParticipantMessageAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken);
    Task<LiveSupportAIWorkerClaimDto?> ClaimAsync(Guid turnId, CancellationToken cancellationToken);
    Task<string> CompleteAsync(Guid turnId, LiveSupportAIWorkerCompletionDto request, CancellationToken cancellationToken);
    Task<string> FailAsync(Guid turnId, LiveSupportAIWorkerFailureDto request, CancellationToken cancellationToken);
}
