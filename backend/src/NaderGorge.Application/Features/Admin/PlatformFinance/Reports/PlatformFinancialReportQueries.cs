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
        var (start, end) = FinancialLedgerQuery.Period(from, to);
        var normalizedKind = kind.Trim().ToLowerInvariant();
        var accounts = await new FinancialLedgerQuery(db).GetAccountsAsync(from, to, ct);
        var closing = normalizedKind is "financial-position" or "financial_position" or "trial-balance" or "trial_balance";
        var rows = accounts.Select(account => new PlatformFinancialReportRow(account.Code, account.Name, account.Type,
            closing ? account.ClosingDebit : account.PeriodDebit,
            closing ? account.ClosingCredit : account.PeriodCredit,
            closing ? account.ClosingBalance : account.PeriodBalance)).ToArray();
        var selected = FilterKind(normalizedKind, rows);
        return new(normalizedKind, start, end.AddTicks(-1), selected.Sum(row => row.Debit), selected.Sum(row => row.Credit), selected);
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
