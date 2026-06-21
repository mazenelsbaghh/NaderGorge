using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public interface ILiveSupportActionExecutor
{
    Task<IReadOnlyList<LiveSupportActionDefinitionDto>> GetCatalogAsync(Guid actorUserId, bool isAdmin, Guid conversationId, CancellationToken ct);
    Task<LiveSupportActionResultDto> ExecuteAsync(LiveSupportActionRequest request, CancellationToken ct);
}
