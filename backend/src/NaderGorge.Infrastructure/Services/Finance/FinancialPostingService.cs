using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

public sealed class FinancialPostingService(IAppDbContext db) : IFinancialPostingService
{
    private readonly IAppDbContext _db = db;

    public async Task<JournalEntry> PostAsync(FinancialPostingRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var existing = await _db.JournalEntries
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return existing;

        var period = await _db.AccountingPeriods
            .Where(x => x.StartDate <= request.OccurredAt.Date && x.EndDate >= request.OccurredAt.Date)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (period?.Status == AccountingPeriodStatus.Closed)
            throw new InvalidOperationException("FINANCE_PERIOD_CLOSED");

        var accountCodes = request.Lines.Select(x => x.AccountCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var accounts = await _db.FinancialAccounts
            .Where(x => accountCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        if (accounts.Count != accountCodes.Length)
            throw new InvalidOperationException("FINANCE_ACCOUNT_NOT_FOUND");

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            SequenceNumber = (await _db.JournalEntries.MaxAsync(x => (long?)x.SequenceNumber, cancellationToken) ?? 0L) + 1L,
            OccurredAt = request.OccurredAt,
            PostedAt = DateTime.UtcNow,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            PostingKind = request.PostingKind,
            IdempotencyKey = request.IdempotencyKey,
            Description = request.Description.Trim(),
            ActorUserId = request.ActorUserId,
            CorrelationId = request.CorrelationId,
            Status = JournalEntryStatus.Posted
        };

        foreach (var line in request.Lines)
        {
            entry.Lines.Add(new JournalLine
            {
                Id = Guid.NewGuid(),
                FinancialAccountId = accounts[line.AccountCode].Id,
                Debit = decimal.Round(line.Debit, 2, MidpointRounding.AwayFromZero),
                Credit = decimal.Round(line.Credit, 2, MidpointRounding.AwayFromZero),
                StudentId = line.StudentId,
                TeacherId = line.TeacherId,
                TreasuryAccountId = line.TreasuryAccountId,
                DimensionKey = line.DimensionKey,
                Memo = line.Memo
            });
        }

        _db.JournalEntries.Add(entry);
        await SaveInTransactionAsync(cancellationToken);
        return entry;
    }

    public async Task<JournalEntry> ReverseAsync(Guid journalEntryId, Guid? actorUserId, string reason, CancellationToken cancellationToken = default)
    {
        var original = await _db.JournalEntries
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == journalEntryId, cancellationToken)
            ?? throw new InvalidOperationException("FINANCE_JOURNAL_NOT_FOUND");
        if (original.Status == JournalEntryStatus.Reversed)
            throw new InvalidOperationException("FINANCE_ALREADY_REVERSED");

        var reversalKey = $"reversal:{original.Id:N}";
        var reversal = await _db.JournalEntries.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.IdempotencyKey == reversalKey, cancellationToken);
        if (reversal is not null) return reversal;

        var accountIds = original.Lines.Select(x => x.FinancialAccountId).Distinct().ToArray();
        var accounts = await _db.FinancialAccounts
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var result = await PostAsync(new FinancialPostingRequest(
            "FinancialJournal",
            original.Id,
            "Reversal",
            reversalKey,
            string.IsNullOrWhiteSpace(reason) ? $"Reversal of journal {original.SequenceNumber}" : reason,
            DateTime.UtcNow,
            actorUserId,
            original.Lines.Select(line => new FinancialPostingLine(
                accounts[line.FinancialAccountId].Code,
                line.Credit,
                line.Debit,
                line.StudentId,
                line.TeacherId,
                line.TreasuryAccountId,
                line.DimensionKey,
                line.Memo)).ToArray()), cancellationToken);

        original.Status = JournalEntryStatus.Reversed;
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task SaveInTransactionAsync(CancellationToken cancellationToken)
    {
        if (_db is not DbContext context
            || context.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL"
            || context.Database.CurrentTransaction is not null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void ValidateRequest(FinancialPostingRequest request)
    {
        if (request.Lines.Count < 2)
            throw new InvalidOperationException("FINANCE_ENTRY_NEEDS_TWO_LINES");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new InvalidOperationException("FINANCE_IDEMPOTENCY_REQUIRED");
        if (request.Lines.Any(x => x.Debit < 0m || x.Credit < 0m || (x.Debit > 0m && x.Credit > 0m) || (x.Debit == 0m && x.Credit == 0m)))
            throw new InvalidOperationException("FINANCE_INVALID_LINE");
        var debit = request.Lines.Sum(x => decimal.Round(x.Debit, 2));
        var credit = request.Lines.Sum(x => decimal.Round(x.Credit, 2));
        if (debit != credit)
            throw new InvalidOperationException("FINANCE_UNBALANCED_ENTRY");
    }
}
