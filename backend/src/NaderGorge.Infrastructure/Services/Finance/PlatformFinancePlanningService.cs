using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

public sealed class PlatformFinancePlanningService(
    IAppDbContext db,
    IFinancialPostingService posting) : IPlatformFinancePlanningService
{
    private readonly IAppDbContext _db = db;
    private readonly IFinancialPostingService _posting = posting;

    public async Task<FinanceBudgetPlan> CreateBudgetAsync(CreateFinanceBudgetRequest request, CancellationToken ct)
    {
        if (request.StartDate.Date > request.EndDate.Date || request.Lines.Any(x => x.PlannedAmount < 0m))
            throw new InvalidOperationException("FINANCE_INVALID_BUDGET");
        if (!Enum.IsDefined((FinanceBudgetPeriodKind)request.PeriodKind))
            throw new InvalidOperationException("FINANCE_INVALID_BUDGET_PERIOD");
        var accountIds = request.Lines.Select(x => x.FinancialAccountId).Distinct().ToArray();
        if (await _db.FinancialAccounts.CountAsync(x => accountIds.Contains(x.Id) && x.IsActive, ct) != accountIds.Length)
            throw new InvalidOperationException("FINANCE_ACCOUNT_NOT_FOUND");

        var budget = new FinanceBudgetPlan
        {
            Name = request.Name.Trim(),
            PeriodKind = (FinanceBudgetPeriodKind)request.PeriodKind,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            CreatedByUserId = request.CreatedByUserId,
            Lines = request.Lines.Select(line => new FinanceBudgetLine
            {
                FinancialAccountId = line.FinancialAccountId,
                CostCenterId = line.CostCenterId,
                TeacherId = line.TeacherId,
                PlannedAmount = decimal.Round(line.PlannedAmount, 2)
            }).ToList()
        };
        _db.FinanceBudgetPlans.Add(budget);
        await _db.SaveChangesAsync(ct);
        return budget;
    }

    public async Task<IReadOnlyList<object>> GetBudgetActualsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var actuals = await _db.JournalLines.AsNoTracking()
            .Where(line => line.JournalEntry.Status == JournalEntryStatus.Posted
                && line.JournalEntry.OccurredAt >= from.Date
                && line.JournalEntry.OccurredAt < to.Date.AddDays(1))
            .GroupBy(line => new { line.FinancialAccountId, line.FinancialAccount.Code, line.FinancialAccount.Name })
            .Select(group => new
            {
                group.Key.FinancialAccountId,
                group.Key.Code,
                group.Key.Name,
                Actual = group.Sum(line => line.Debit - line.Credit)
            })
            .Cast<object>()
            .ToListAsync(ct);
        return actuals;
    }

    public async Task<TreasuryTransfer> TransferAsync(TreasuryTransferRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0m || request.SourceTreasuryAccountId == request.DestinationTreasuryAccountId)
            throw new InvalidOperationException("FINANCE_INVALID_TRANSFER");
        var accounts = await (from treasury in _db.TreasuryAccounts
                              join account in _db.FinancialAccounts on treasury.FinancialAccountId equals account.Id
                              where treasury.Id == request.SourceTreasuryAccountId || treasury.Id == request.DestinationTreasuryAccountId
                              select new { treasury.Id, Code = account.Code }).ToListAsync(ct);
        var source = accounts.SingleOrDefault(x => x.Id == request.SourceTreasuryAccountId)?.Code ?? throw new InvalidOperationException("FINANCE_TREASURY_NOT_FOUND");
        var destination = accounts.SingleOrDefault(x => x.Id == request.DestinationTreasuryAccountId)?.Code ?? throw new InvalidOperationException("FINANCE_TREASURY_NOT_FOUND");
        var journal = await _posting.PostAsync(new FinancialPostingRequest(
            "TreasuryTransfer", null, "Transfer", request.IdempotencyKey, request.Reference, DateTime.UtcNow,
            request.ActorUserId,
            [new FinancialPostingLine(destination, request.Amount, 0m, TreasuryAccountId: request.DestinationTreasuryAccountId), new FinancialPostingLine(source, 0m, request.Amount, TreasuryAccountId: request.SourceTreasuryAccountId)]), ct);
        var transfer = new TreasuryTransfer
        {
            SourceTreasuryAccountId = request.SourceTreasuryAccountId,
            DestinationTreasuryAccountId = request.DestinationTreasuryAccountId,
            Amount = request.Amount,
            Reference = request.Reference.Trim(),
            JournalEntryId = journal.Id,
            CreatedByUserId = request.ActorUserId
        };
        _db.TreasuryTransfers.Add(transfer);
        await _db.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task<TreasuryReconciliation> ReconcileAsync(TreasuryReconciliationRequest request, CancellationToken ct)
    {
        var treasury = await _db.TreasuryAccounts.FindAsync([request.TreasuryAccountId], ct)
            ?? throw new InvalidOperationException("FINANCE_TREASURY_NOT_FOUND");
        var systemBalance = await (from line in _db.JournalLines
                                   join entry in _db.JournalEntries on line.JournalEntryId equals entry.Id
                                   where entry.Status == JournalEntryStatus.Posted
                                       && line.TreasuryAccountId == request.TreasuryAccountId
                                       && entry.OccurredAt <= request.AsOfDate
                                   select line.Debit - line.Credit).SumAsync(ct);
        var reconciliation = new TreasuryReconciliation
        {
            TreasuryAccountId = treasury.Id,
            AsOfDate = request.AsOfDate.Date,
            SystemBalance = systemBalance,
            CountedOrStatementBalance = request.CountedOrStatementBalance,
            EvidenceNote = request.EvidenceNote.Trim(),
            CreatedByUserId = request.ActorUserId
        };
        _db.TreasuryReconciliations.Add(reconciliation);
        await _db.SaveChangesAsync(ct);
        return reconciliation;
    }
}
