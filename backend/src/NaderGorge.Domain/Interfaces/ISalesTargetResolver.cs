using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public sealed record SalesTargetContext(
    SalesTargetType TargetType,
    Guid? TargetId,
    decimal Price,
    Guid? TeacherId,
    Guid? SubjectId,
    string? GradeLevel,
    Guid? VideoTypeId,
    bool IsSaleEligible,
    string DisplayName
);

public interface ISalesTargetResolver
{
    Task<SalesTargetContext?> ResolveAsync(SalesTargetType targetType, Guid? targetId, CancellationToken cancellationToken = default);
    Task<SalesTargetContext?> ResolveFromCodeTypeAsync(CodeType contentType, Guid contentId, CancellationToken cancellationToken = default);
}
