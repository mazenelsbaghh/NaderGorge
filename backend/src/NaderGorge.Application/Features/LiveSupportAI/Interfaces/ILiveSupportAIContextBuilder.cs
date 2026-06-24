using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIContextBuilder
{
    Task<LiveSupportAIWorkerClaimDto> BuildAsync(Guid turnId, CancellationToken cancellationToken);
}
