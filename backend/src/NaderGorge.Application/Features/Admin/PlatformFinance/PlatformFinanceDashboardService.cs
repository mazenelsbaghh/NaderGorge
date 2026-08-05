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
    decimal Outstanding);

public sealed class PlatformFinanceDashboardService(IAppDbContext db)
{
    private readonly IAppDbContext _db = db;

    public async Task<PlatformFinanceDashboardDto> GetDashboardAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var start = (from ?? DateTime.UtcNow.Date.AddMonths(-1)).Date;
        var end = (to ?? DateTime.UtcNow.Date).Date.AddDays(1).AddTicks(-1);
        if (end < start) throw new ArgumentException("The report end date must be after the start date.");

        var groupedAccounts = await _db.JournalLines.AsNoTracking()
            .Where(line => line.JournalEntry.Status == JournalEntryStatus.Posted
                && line.JournalEntry.OccurredAt >= start
                && line.JournalEntry.OccurredAt <= end)
            .GroupBy(line => new
            {
                line.FinancialAccountId,
                line.FinancialAccount.Code,
                line.FinancialAccount.Name,
                line.FinancialAccount.Type,
                line.FinancialAccount.Role
            })
            .Select(group => new { group.Key.FinancialAccountId, group.Key.Code, group.Key.Name, group.Key.Type, Debit = group.Sum(line => line.Debit), Credit = group.Sum(line => line.Credit) })
            .ToListAsync(ct);
        var rows = groupedAccounts
            .Select(row => new PlatformFinanceAccountBalanceDto(row.FinancialAccountId, row.Code, row.Name, row.Type, row.Debit, row.Credit,
                row.Type is FinancialAccountType.Asset or FinancialAccountType.Expense or FinancialAccountType.ContraRevenue ? row.Debit - row.Credit : row.Credit - row.Debit))
            .OrderBy(row => row.Code)
            .ToArray();

        // Resolve roles in the same query shape so dashboard calculations never
        // depend on translated string account names.
        var groupedRoles = await _db.JournalLines.AsNoTracking()
            .Where(line => line.JournalEntry.Status == JournalEntryStatus.Posted
                && line.JournalEntry.OccurredAt >= start
                && line.JournalEntry.OccurredAt <= end)
            .GroupBy(line => line.FinancialAccount.Role)
            .Select(group => new { Role = group.Key, Debit = group.Sum(line => line.Debit), Credit = group.Sum(line => line.Credit) })
            .ToListAsync(ct);
        var roleTypes = await _db.FinancialAccounts.AsNoTracking().Select(account => new { account.Role, account.Type }).ToListAsync(ct);
        var roleBalances = groupedRoles.ToDictionary(item => item.Role, item =>
        {
            var type = roleTypes.FirstOrDefault(candidate => candidate.Role == item.Role)?.Type;
            return type is FinancialAccountType.Asset or FinancialAccountType.Expense or FinancialAccountType.ContraRevenue ? item.Debit - item.Credit : item.Credit - item.Debit;
        });

        decimal GetRole(FinancialAccountRole role) => roleBalances.GetValueOrDefault(role);
        var revenue = GetRole(FinancialAccountRole.PlatformRevenue);
        var refunds = GetRole(FinancialAccountRole.Refunds);
        var expenses = GetRole(FinancialAccountRole.OperatingExpense) + GetRole(FinancialAccountRole.PayrollExpense);

        return new PlatformFinanceDashboardDto(
            start,
            end,
            GetRole(FinancialAccountRole.Treasury),
            GetRole(FinancialAccountRole.GeneralStudentLiability),
            GetRole(FinancialAccountRole.TeacherStudentLiability),
            GetRole(FinancialAccountRole.TeacherPayable),
            GetRole(FinancialAccountRole.SupplierPayable),
            revenue,
            refunds,
            expenses,
            revenue - refunds - expenses,
            rows);
    }

    public async Task<IReadOnlyList<PlatformFinanceJournalDto>> GetLedgerAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct)
    {
        var start = (from ?? DateTime.UtcNow.Date.AddMonths(-1)).Date;
        var end = (to ?? DateTime.UtcNow.Date).Date.AddDays(1).AddTicks(-1);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var entries = await _db.JournalEntries.AsNoTracking()
            .Include(entry => entry.Lines)
            .ThenInclude(line => line.FinancialAccount)
            .Where(entry => entry.Status == JournalEntryStatus.Posted && entry.OccurredAt >= start && entry.OccurredAt <= end)
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
        var start = (from ?? DateTime.UtcNow.Date.AddMonths(-1)).Date;
        var end = (to ?? DateTime.UtcNow.Date).Date.AddDays(1);
        var rows = await _db.JournalLines.AsNoTracking()
            .Where(line => line.TeacherId.HasValue
                && line.JournalEntry.Status == JournalEntryStatus.Posted
                && line.JournalEntry.OccurredAt >= start
                && line.JournalEntry.OccurredAt < end)
            .GroupBy(line => new { TeacherId = line.TeacherId!.Value, line.FinancialAccount.Role, line.JournalEntry.SourceType })
            .Select(group => new
            {
                group.Key.TeacherId,
                group.Key.Role,
                group.Key.SourceType,
                Amount = group.Sum(line => line.Credit - line.Debit)
            })
            .ToListAsync(ct);

        var teacherIds = rows.Select(item => item.TeacherId).Distinct().ToArray();
        var names = await _db.TeacherProfiles.AsNoTracking()
            .Where(teacher => teacherIds.Contains(teacher.Id))
            .ToDictionaryAsync(teacher => teacher.Id, teacher => teacher.User.FullName, ct);

        return rows.GroupBy(item => item.TeacherId)
            .Select(group =>
            {
                decimal Amount(FinancialAccountRole role) => group.Where(item => item.Role == role).Sum(item => item.Amount);
                var teacherShare = Amount(FinancialAccountRole.TeacherPayable);
                var refunds = group.Where(item => item.Role == FinancialAccountRole.Refunds).Sum(item => -item.Amount);
                var paid = group.Where(item => item.SourceType != "PlatformRefund").Sum(item => Math.Max(0m, item.Amount));
                return new PlatformFinanceTeacherSummaryDto(
                    group.Key,
                    names.GetValueOrDefault(group.Key, "مدرس غير معروف"),
                    paid + refunds,
                    Amount(FinancialAccountRole.PlatformRevenue),
                    teacherShare,
                    refunds,
                    paid,
                    teacherShare);
            })
            .OrderByDescending(item => item.Outstanding)
            .ToArray();
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
