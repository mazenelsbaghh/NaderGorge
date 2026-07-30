using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIAdminService
{
    LiveSupportAICatalogsDto GetCatalogs();
    Task<LiveSupportAIConfigDto> GetConfigAsync(CancellationToken cancellationToken);
    Task<LiveSupportAIPolicyDto> SaveDraftAsync(Guid adminUserId, SaveLiveSupportAIDraftRequest request, CancellationToken cancellationToken);
    Task<LiveSupportAIPolicyDto> PublishAsync(Guid adminUserId, long expectedVersion, CancellationToken cancellationToken);
    Task DisableAsync(Guid adminUserId, long expectedVersion, CancellationToken cancellationToken);
    Task<LiveSupportAIPolicyDto> EnableAsync(Guid adminUserId, long expectedVersion, CancellationToken cancellationToken);
    Task<LiveSupportAIStatsDto> GetStatsAsync(string period, CancellationToken cancellationToken);
    Task<IReadOnlyList<LiveSupportAdminConversationDto>> GetActiveConversationsAsync(CancellationToken cancellationToken);
    Task<LiveSupportAIPreviewResultDto> PreviewAsync(LiveSupportAIPreviewRequestDto request, CancellationToken cancellationToken);
    Task<LiveSupportAIEvidencePageDto> GetEvidenceAsync(string period, string? cursor, int pageSize, CancellationToken cancellationToken);
}

public sealed class LiveSupportAIAdminException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
