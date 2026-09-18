using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance;

public sealed record PlatformProfitTeacherRow(TeacherFinancialSummaryDto Period,
    decimal CurrentAccountBalance, decimal CurrentLedgerBalance, decimal ReconciliationDifference,
    decimal CurrentCalculatedBalance);
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
        var nowEnd = CairoTime.GetRollingMonthRangeUtc(today, today).EndUtc;
        var history = await new ProfitSalesHistory(db).ReadAsync(end > nowEnd ? end : nowEnd, ct);
        var allPaid = (await teacherSummary.GetAllAsync(new DateTime(2000, 1, 1), to ?? today, ct))
            .ToDictionary(x => x.TeacherId, x => x.Paid);
        var currentPaid = (await teacherSummary.GetAllAsync(new DateTime(2000, 1, 1), today, ct))
            .ToDictionary(x => x.TeacherId, x => x.Paid);
        var byTeacher = history.ToLookup(x => x.TeacherId);
        var accounts = await db.TeacherAccounts.AsNoTracking().ToDictionaryAsync(x => x.TeacherId, x => x.CurrentBalance, ct);
        var teachers = await db.TeacherProfiles.AsNoTracking().Select(x => new { x.Id, x.User.FullName }).ToListAsync(ct);
        var firstEntry = history.Select(x => (DateTime?)x.OccurredAt).Min();
        var rows = teachers.Select(teacher =>
        {
            var summary = period.GetValueOrDefault(teacher.Id)
                ?? new TeacherFinancialSummaryDto(teacher.Id, teacher.FullName, 0, 0, 0, 0, 0, 0);
            var movements = byTeacher[teacher.Id].ToArray();
            var selected = movements.Where(x => x.OccurredAt >= start && x.OccurredAt < end).ToArray();
            summary = summary with {
                GrossSales = selected.Sum(x => x.Sales), TeacherShare = selected.Sum(x => x.TeacherShare),
                PlatformShare = selected.Sum(x => x.PlatformShare), Refunds = selected.Sum(x => x.Refunds),
                Outstanding = movements.Where(x => x.OccurredAt < end).Sum(x => x.TeacherShare) - allPaid.GetValueOrDefault(teacher.Id)
            };
            var calculated = movements.Where(x => x.OccurredAt < nowEnd).Sum(x => x.TeacherShare) - currentPaid.GetValueOrDefault(teacher.Id);
            var account = accounts.GetValueOrDefault(teacher.Id);
            var ledger = currentLedger.GetValueOrDefault(teacher.Id);
            return new PlatformProfitTeacherRow(summary, account, ledger, calculated - account, calculated);
        }).OrderByDescending(x => x.Period.PlatformShare).ThenBy(x => x.Period.TeacherName).ToArray();
        var selectedHistory = history.Where(x => x.OccurredAt >= start && x.OccurredAt < end).ToArray();
        var revenue = selectedHistory.Sum(x => Math.Max(0m, x.PlatformShare));
        var refunds = selectedHistory.Sum(x => Math.Max(0m, -x.PlatformShare));
        platform = platform with { Revenue = revenue, Refunds = refunds, NetProfit = revenue - refunds - platform.Expenses };
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(DateTime.UtcNow, CairoTime.ToLocal(firstEntry ?? DateTime.UtcNow).ToString("yyyy-MM-dd"), platform, rows);
    }
}
