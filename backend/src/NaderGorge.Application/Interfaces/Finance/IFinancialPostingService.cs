using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Interfaces.Finance;

public sealed record FinancialPostingLine(
    string AccountCode,
    decimal Debit,
    decimal Credit,
    Guid? StudentId = null,
    Guid? TeacherId = null,
    Guid? TreasuryAccountId = null,
    string? DimensionKey = null,
    string? Memo = null);

public sealed record FinancialPostingRequest(
    string SourceType,
    Guid? SourceId,
    string PostingKind,
    string IdempotencyKey,
    string Description,
    DateTime OccurredAt,
    Guid? ActorUserId,
    IReadOnlyCollection<FinancialPostingLine> Lines,
    string? CorrelationId = null);

public interface IFinancialPostingService
{
    Task<JournalEntry> PostAsync(FinancialPostingRequest request, CancellationToken cancellationToken = default);
    Task<JournalEntry> ReverseAsync(Guid journalEntryId, Guid? actorUserId, string reason, CancellationToken cancellationToken = default);
}
