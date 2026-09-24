using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance;

public sealed record PlatformFinanceDashboardDto(
    DateTime From,
    DateTime To,
    decimal Cash,
    decimal GeneralStudentLiability,
    decimal TeacherStudentLiability,
    decimal TeacherPayable,
    decimal SupplierPayable,
    decimal Revenue,
    decimal Refunds,
    decimal Expenses,
    decimal NetProfit,
    IReadOnlyList<PlatformFinanceAccountBalanceDto> Accounts);

public sealed record PlatformFinanceAccountBalanceDto(
    Guid AccountId,
    string Code,
    string Name,
    FinancialAccountType Type,
    decimal Debit,
    decimal Credit,
    decimal Balance);

public sealed record PlatformFinanceJournalLineDto(
    Guid Id,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit,
    Guid? StudentId,
    Guid? TeacherId,
    Guid? TreasuryAccountId,
    string? Memo);

public sealed record PlatformFinanceJournalDto(
    Guid Id,
    long SequenceNumber,
    DateTime OccurredAt,
    DateTime PostedAt,
    string SourceType,
    Guid? SourceId,
    string PostingKind,
    string Description,
    IReadOnlyList<PlatformFinanceJournalLineDto> Lines);

public sealed record PlatformFinanceTeacherSummaryDto(
    Guid TeacherId,
    string TeacherName,
    decimal GrossSales,
    decimal PlatformShare,
    decimal TeacherShare,
    decimal Refunds,
    decimal Paid,
    decimal Outstanding, decimal Adjustments, NaderGorge.Application.Services.TeacherFinanceAccountSnapshot? Account = null);

public sealed class PlatformFinanceDashboardService(IAppDbContext db)
{
    private readonly IAppDbContext _db = db;

    public async Task<PlatformFinanceDashboardDto> GetDashboardAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (start, end) = FinancialLedgerQuery.Period(from, to);
        var accounts = await new FinancialLedgerQuery(_db).GetAccountsAsync(from, to, ct);
        var rows = accounts.Select(account => new PlatformFinanceAccountBalanceDto(
            account.AccountId, account.Code, account.Name, account.Type,
            account.IsBalanceSheet ? account.ClosingDebit : account.PeriodDebit,
            account.IsBalanceSheet ? account.ClosingCredit : account.PeriodCredit,
            account.IsBalanceSheet ? account.ClosingBalance : account.PeriodBalance)).ToArray();
        decimal Closing(FinancialAccountRole role) => accounts.Where(account => account.Role == role).Sum(account => account.ClosingBalance);
        decimal Period(FinancialAccountType type) => accounts.Where(account => account.Type == type).Sum(account => account.PeriodBalance);
        var revenue = Period(FinancialAccountType.Revenue);
        var refunds = Period(FinancialAccountType.ContraRevenue);
        var expenses = Period(FinancialAccountType.Expense);
        return new PlatformFinanceDashboardDto(start, end.AddTicks(-1),
            Closing(FinancialAccountRole.Treasury), Closing(FinancialAccountRole.GeneralStudentLiability),
            Closing(FinancialAccountRole.TeacherStudentLiability), Closing(FinancialAccountRole.TeacherPayable),
            Closing(FinancialAccountRole.SupplierPayable), revenue, refunds, expenses,
            revenue - refunds - expenses, rows);
    }

    public async Task<IReadOnlyList<PlatformFinanceJournalDto>> GetLedgerAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct)
    {
        var (start, end) = FinancialLedgerQuery.Period(from, to);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var entries = await new FinancialLedgerQuery(_db).Entries
            .Include(entry => entry.Lines)
            .ThenInclude(line => line.FinancialAccount)
            .Where(entry => entry.OccurredAt >= start && entry.OccurredAt < end)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.SequenceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return entries.Select(entry => new PlatformFinanceJournalDto(
            entry.Id,
            entry.SequenceNumber,
            entry.OccurredAt,
            entry.PostedAt,
            entry.SourceType,
            entry.SourceId,
            entry.PostingKind,
            entry.Description,
            entry.Lines.Select(line => new PlatformFinanceJournalLineDto(
                line.Id,
                line.FinancialAccountId,
                line.FinancialAccount.Code,
                line.FinancialAccount.Name,
                line.Debit,
                line.Credit,
                line.StudentId,
                line.TeacherId,
                line.TreasuryAccountId,
                line.Memo)).ToArray())).ToArray();
    }

    public async Task<PlatformFinanceJournalDto?> GetJournalAsync(Guid journalId, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.AsNoTracking()
            .Include(item => item.Lines)
            .ThenInclude(line => line.FinancialAccount)
            .SingleOrDefaultAsync(item => item.Id == journalId, ct);
        return entry is null ? null : MapEntry(entry);
    }

    public async Task<IReadOnlyList<PlatformFinanceTeacherSummaryDto>> GetTeacherSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var summaries = await new Teachers.GetTeacherFinancialSummaryQuery(_db).GetAllAsync(from, to, ct);
        return summaries.Select(x => new PlatformFinanceTeacherSummaryDto(x.TeacherId, x.TeacherName,
            x.GrossSales, x.PlatformShare, x.TeacherShare, x.Refunds, x.Paid, x.Outstanding, x.Adjustments)).ToArray();
    }

    private static PlatformFinanceJournalDto MapEntry(Domain.Entities.JournalEntry entry) => new(
        entry.Id,
        entry.SequenceNumber,
        entry.OccurredAt,
        entry.PostedAt,
        entry.SourceType,
        entry.SourceId,
        entry.PostingKind,
        entry.Description,
        entry.Lines.Select(line => new PlatformFinanceJournalLineDto(
            line.Id,
            line.FinancialAccountId,
            line.FinancialAccount.Code,
            line.FinancialAccount.Name,
            line.Debit,
            line.Credit,
            line.StudentId,
            line.TeacherId,
            line.TreasuryAccountId,
            line.Memo)).ToArray());
}
