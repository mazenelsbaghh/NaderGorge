using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIRecoveryService
{
    Task<LiveSupportAIRecoveryBatchResultDto> RecoverBatchAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken);
}
