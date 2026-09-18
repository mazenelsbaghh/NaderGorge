using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance;

public sealed record PlatformProfitTeacherRow(TeacherFinancialSummaryDto Period,
    decimal CurrentAccountBalance, decimal CurrentLedgerBalance, decimal ReconciliationDifference);
public sealed record PlatformProfitReportDto(DateTime GeneratedAt, string EarliestDate,
    PlatformFinanceDashboardDto Platform, IReadOnlyList<PlatformProfitTeacherRow> Teachers);

public sealed class PlatformProfitReportQuery(IAppDbContext db,
    PlatformFinanceDashboardService dashboard, GetTeacherFinancialSummaryQuery teacherSummary)
{
    public async Task<PlatformProfitReportDto> GetAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (start, end) = CairoTime.GetRollingMonthRangeUtc(from, to);
        if (end <= start) throw new ArgumentException("The report end date must not precede its start date.");
        await using var transaction = (db as DbContext)?.Database.CurrentTransaction is null
            ? await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var platform = await dashboard.GetDashboardAsync(from, to, ct);
        var period = (await teacherSummary.GetAllAsync(from, to, ct)).ToDictionary(x => x.TeacherId);
        var today = CairoTime.ToLocal(DateTime.UtcNow).Date;
        var currentLedger = (await teacherSummary.GetAllAsync(today, today, ct)).ToDictionary(x => x.TeacherId, x => x.Outstanding);
        var accounts = await db.TeacherAccounts.AsNoTracking().ToDictionaryAsync(x => x.TeacherId, x => x.CurrentBalance, ct);
        var teachers = await db.TeacherProfiles.AsNoTracking().Select(x => new { x.Id, x.User.FullName }).ToListAsync(ct);
        var firstEntry = await db.JournalEntries.AsNoTracking().Where(x => x.Status == JournalEntryStatus.Posted)
            .MinAsync(x => (DateTime?)x.OccurredAt, ct);
        var rows = teachers.Select(teacher =>
        {
            var summary = period.GetValueOrDefault(teacher.Id)
                ?? new TeacherFinancialSummaryDto(teacher.Id, teacher.FullName, 0, 0, 0, 0, 0, 0);
            var account = accounts.GetValueOrDefault(teacher.Id);
            var ledger = currentLedger.GetValueOrDefault(teacher.Id);
            return new PlatformProfitTeacherRow(summary, account, ledger, ledger - account);
        }).OrderByDescending(x => x.Period.PlatformShare).ThenBy(x => x.Period.TeacherName).ToArray();
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(DateTime.UtcNow, CairoTime.ToLocal(firstEntry ?? DateTime.UtcNow).ToString("yyyy-MM-dd"), platform, rows);
    }
}
