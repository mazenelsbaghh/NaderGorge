using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public interface IContentArchiveAccessService
{
    Task<bool> CanViewAsync(Guid userId, ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
    Task<bool> CanAcquireAsync(ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
}
