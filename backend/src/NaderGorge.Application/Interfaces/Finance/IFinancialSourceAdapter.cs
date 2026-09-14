using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Interfaces.Finance;

public sealed record FinanceSourcePostingRequest(string SourceType, Guid SourceId, Guid StudentId, Guid? TeacherId, decimal Amount, decimal PlatformAmount, decimal TeacherAmount, DateTime OccurredAt, Guid? ActorUserId, string IdempotencyKey);

public interface IFinancialSourceAdapter
{
    bool CanHandle(string sourceType);
    Task<JournalEntry> PostAsync(FinanceSourcePostingRequest request, CancellationToken ct);
}

public interface ILiveFinancialProjectionCoordinator
{
    Task<JournalEntry> PostSourceAsync(FinanceSourcePostingRequest request, CancellationToken ct);
}
