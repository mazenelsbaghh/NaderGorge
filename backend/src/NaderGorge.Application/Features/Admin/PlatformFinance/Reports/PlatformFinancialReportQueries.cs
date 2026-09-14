using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Reports;

public sealed record PlatformFinancialReportRow(string Code, string Name, FinancialAccountType Type, decimal Debit, decimal Credit, decimal Balance);
public sealed record PlatformFinancialReportDto(string Kind, DateTime From, DateTime To, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<PlatformFinancialReportRow> Rows);

/// <summary>All accountant-facing reports use one bounded, posted-journal dataset.</summary>
public sealed class PlatformFinancialReportQueries(IAppDbContext db)
{
    public async Task<PlatformFinancialReportDto> GetAsync(string kind, DateTime from, DateTime to, CancellationToken ct)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);
        if (end <= start) throw new ArgumentException("The report end date must be after the start date.");
        var normalizedKind = kind.Trim().ToLowerInvariant();
        var raw = await db.JournalLines.AsNoTracking()
            .Where(line => line.JournalEntry.Status == JournalEntryStatus.Posted && line.JournalEntry.OccurredAt >= start && line.JournalEntry.OccurredAt < end)
            .GroupBy(line => new { line.FinancialAccount.Code, line.FinancialAccount.Name, line.FinancialAccount.Type })
            .Select(group => new { group.Key.Code, group.Key.Name, group.Key.Type, Debit = group.Sum(line => line.Debit), Credit = group.Sum(line => line.Credit) })
            .OrderBy(row => row.Code)
            .ToListAsync(ct);

        var rows = raw.Select(row => new PlatformFinancialReportRow(
            row.Code,
            row.Name,
            row.Type,
            row.Debit,
            row.Credit,
            row.Type is FinancialAccountType.Asset or FinancialAccountType.Expense or FinancialAccountType.ContraRevenue
                ? row.Debit - row.Credit
                : row.Credit - row.Debit)).ToArray();

        return new(normalizedKind, start, end.AddTicks(-1), rows.Sum(row => row.Debit), rows.Sum(row => row.Credit), FilterKind(normalizedKind, rows));
    }

    private static IReadOnlyList<PlatformFinancialReportRow> FilterKind(string kind, IReadOnlyList<PlatformFinancialReportRow> rows) => kind switch
    {
        "profit-loss" or "profit_loss" => rows.Where(row => row.Type is FinancialAccountType.Revenue or FinancialAccountType.ContraRevenue or FinancialAccountType.Expense).ToArray(),
        "cash-flow" or "cash_flow" => rows.Where(row => row.Type == FinancialAccountType.Asset && row.Code.StartsWith("1", StringComparison.Ordinal)).ToArray(),
        "financial-position" or "financial_position" => rows.Where(row => row.Type is FinancialAccountType.Asset or FinancialAccountType.Liability or FinancialAccountType.Equity).ToArray(),
        "refunds" => rows.Where(row => row.Type == FinancialAccountType.ContraRevenue || row.Code == "4100").ToArray(),
        "expenses" => rows.Where(row => row.Type == FinancialAccountType.Expense).ToArray(),
        _ => rows
    };
}
