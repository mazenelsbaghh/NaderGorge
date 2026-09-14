using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public interface IGiftUsageService
{
    Task<bool> TryConsumeAsync(
        Guid studentId,
        GiftTargetType targetType,
        Guid targetId,
        CancellationToken ct = default);
}
