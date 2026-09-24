using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public interface IContentArchiveAccessService
{
    Task<bool> CanViewAsync(Guid userId, ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> GetViewableLessonIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> GetViewableLessonVideoIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonVideoIds, CancellationToken cancellationToken = default);
    async Task<IReadOnlySet<Guid>> GetViewableAssessmentIdsAsync(Guid userId, ContentArchiveTargetType targetType, IReadOnlyCollection<Guid> targetIds, CancellationToken cancellationToken = default)
    {
        var viewableIds = new HashSet<Guid>();
        foreach (var targetId in targetIds.Distinct())
            if (await CanViewAsync(userId, targetType, targetId, cancellationToken))
                viewableIds.Add(targetId);
        return viewableIds;
    }
    Task<bool> CanAcquireAsync(ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
}
