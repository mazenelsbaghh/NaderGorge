using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance;

public sealed record FinancialAccountTotals(Guid AccountId, string Code, string Name,
    FinancialAccountType Type, FinancialAccountRole Role, decimal PeriodDebit, decimal PeriodCredit,
    decimal ClosingDebit, decimal ClosingCredit)
{
    public bool IsBalanceSheet => Type is FinancialAccountType.Asset or FinancialAccountType.Liability or FinancialAccountType.Equity;
    public decimal PeriodBalance => Balance(PeriodDebit, PeriodCredit);
    public decimal ClosingBalance => Balance(ClosingDebit, ClosingCredit);
    public decimal Balance(decimal debit, decimal credit) =>
        Type is FinancialAccountType.Asset or FinancialAccountType.Expense or FinancialAccountType.ContraRevenue
            ? debit - credit : credit - debit;
}

/// <summary>Shared accounting boundaries and balances for every ledger-backed report.</summary>
public sealed class FinancialLedgerQuery(IAppDbContext db)
{
    // Reversed entries remain part of history; their separate reversal cancels them on its own date.
    public IQueryable<JournalEntry> Entries => db.JournalEntries.AsNoTracking()
        .Where(entry => entry.Status == JournalEntryStatus.Posted || entry.Status == JournalEntryStatus.Reversed);
    public IQueryable<JournalLine> Lines => db.JournalLines.AsNoTracking()
        .Where(line => line.JournalEntry.Status == JournalEntryStatus.Posted || line.JournalEntry.Status == JournalEntryStatus.Reversed);

    public static (DateTime StartUtc, DateTime EndUtc) Period(DateTime? from, DateTime? to)
    {
        var period = CairoTime.GetRollingMonthRangeUtc(from, to);
        if (period.EndUtc <= period.StartUtc)
            throw new ArgumentException("The report end date must not precede its start date.");
        return period;
    }

    public async Task<IReadOnlyList<FinancialAccountTotals>> GetAccountsAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (start, end) = Period(from, to);
        return await Lines.Where(line => line.JournalEntry.OccurredAt < end)
            .GroupBy(line => new { line.FinancialAccountId, line.FinancialAccount.Code, line.FinancialAccount.Name,
                line.FinancialAccount.Type, line.FinancialAccount.Role })
            .OrderBy(group => group.Key.Code)
            .Select(group => new FinancialAccountTotals(group.Key.FinancialAccountId, group.Key.Code, group.Key.Name,
                group.Key.Type, group.Key.Role,
                group.Sum(line => line.JournalEntry.OccurredAt >= start ? line.Debit : 0m),
                group.Sum(line => line.JournalEntry.OccurredAt >= start ? line.Credit : 0m),
                group.Sum(line => line.Debit), group.Sum(line => line.Credit)))
            .ToListAsync(ct);
    }
}
