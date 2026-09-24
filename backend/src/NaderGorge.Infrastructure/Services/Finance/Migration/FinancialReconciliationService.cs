using NaderGorge.Application.Features.Admin.PlatformFinance;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance.Migration;

public sealed record FinancialReconciliationRow(string SourceType, DateTime Month, long JournalCount, decimal Debit, decimal Credit, decimal Variance);
public sealed record FinancialReconciliationReport(DateTime From, DateTime To, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<FinancialReconciliationRow> Rows, IReadOnlyList<string> Exceptions);

/// <summary>Read-only reconciliation view; it never mutates source or ledger history.</summary>
public sealed class FinancialReconciliationService(IAppDbContext db)
{
    public async Task<FinancialReconciliationReport> GetAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var (start, end) = FinancialLedgerQuery.Period(from, to);
        var rows = await new FinancialLedgerQuery(db).Entries
            .Where(entry => entry.OccurredAt >= start && entry.OccurredAt < end)
            .SelectMany(entry => entry.Lines.Select(line => new { entry.SourceType, Month = new DateTime(entry.OccurredAt.Year, entry.OccurredAt.Month, 1), line.Debit, line.Credit }))
            .GroupBy(row => new { row.SourceType, row.Month })
            .Select(group => new FinancialReconciliationRow(group.Key.SourceType, group.Key.Month, group.Count(), group.Sum(row => row.Debit), group.Sum(row => row.Credit), group.Sum(row => row.Debit) - group.Sum(row => row.Credit)))
            .OrderBy(row => row.Month).ThenBy(row => row.SourceType)
            .ToListAsync(ct);
        var exceptions = rows.Where(row => row.Variance != 0m).Select(row => $"{row.SourceType}/{row.Month:yyyy-MM}: variance {row.Variance:N2}").ToArray();
        return new(start, end.AddTicks(-1), rows.Sum(row => row.Debit), rows.Sum(row => row.Credit), rows, exceptions);
    }
}
