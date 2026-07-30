using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIWorkerPreviewClient
{
    Task<LiveSupportAIWorkerPreviewResultDto> PreviewAsync(
        LiveSupportAIWorkerClaimDto context,
        CancellationToken cancellationToken);
}
