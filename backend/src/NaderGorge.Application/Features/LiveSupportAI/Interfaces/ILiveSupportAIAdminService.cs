using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIAdminService
{
    LiveSupportAICatalogsDto GetCatalogs();
    Task<LiveSupportAIConfigDto> GetConfigAsync(CancellationToken cancellationToken);
    Task<LiveSupportAIPolicyDto> SaveDraftAsync(Guid adminUserId, SaveLiveSupportAIDraftRequest request, CancellationToken cancellationToken);
    Task<LiveSupportAIPolicyDto> PublishAsync(Guid adminUserId, long expectedVersion, CancellationToken cancellationToken);
    Task DisableAsync(Guid adminUserId, CancellationToken cancellationToken);
    Task<LiveSupportAIPolicyDto> EnableAsync(Guid adminUserId, CancellationToken cancellationToken);
    Task<LiveSupportAIStatsDto> GetStatsAsync(string period, CancellationToken cancellationToken);
    Task<IReadOnlyList<LiveSupportAdminConversationDto>> GetActiveConversationsAsync(CancellationToken cancellationToken);
}

public sealed class LiveSupportAIAdminException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
