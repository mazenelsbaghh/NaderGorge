using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public interface IContentArchiveAccessService
{
    Task<bool> CanViewAsync(Guid userId, ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> GetViewableLessonIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> GetViewableLessonVideoIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonVideoIds, CancellationToken cancellationToken = default);
    Task<bool> CanAcquireAsync(ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
}
