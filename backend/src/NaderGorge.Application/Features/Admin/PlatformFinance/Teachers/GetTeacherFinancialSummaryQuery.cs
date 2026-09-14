using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;

public sealed record TeacherFinancialSummaryDto(Guid TeacherId, string TeacherName, decimal GrossSales, decimal PlatformShare, decimal TeacherShare, decimal Refunds, decimal Paid, decimal Outstanding);

public sealed class GetTeacherFinancialSummaryQuery(IAppDbContext db)
{
    public async Task<TeacherFinancialSummaryDto?> GetAsync(Guid teacherId, DateTime? from, DateTime? to, CancellationToken ct) =>
        (await ReadAsync(teacherId, from, to, ct)).SingleOrDefault();

    public Task<IReadOnlyList<TeacherFinancialSummaryDto>> GetAllAsync(DateTime? from, DateTime? to, CancellationToken ct) =>
        ReadAsync(null, from, to, ct);

    private async Task<IReadOnlyList<TeacherFinancialSummaryDto>> ReadAsync(Guid? teacherId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (start, end) = CairoTime.GetRollingMonthRangeUtc(from, to);
        // Older single-teacher entries omitted the teacher dimension on platform lines.
        // Shared entries without a dimension cannot safely be assigned to either teacher.
        var attributed = db.JournalLines.AsNoTracking()
            .Where(line => line.JournalEntry.Status == JournalEntryStatus.Posted && line.JournalEntry.OccurredAt < end
                && (line.TeacherId != null ||
                    ((line.FinancialAccount.Role == FinancialAccountRole.PlatformRevenue || line.FinancialAccount.Role == FinancialAccountRole.Refunds)
                        && line.JournalEntry.Lines.Where(x => x.TeacherId != null).Select(x => x.TeacherId).Distinct().Count() == 1)))
            .Select(line => new
            {
                TeacherId = line.TeacherId ?? line.JournalEntry.Lines.Where(x => x.TeacherId != null).Select(x => x.TeacherId).FirstOrDefault(),
                line.FinancialAccount.Role, line.JournalEntry.SourceType,
                InPeriod = line.JournalEntry.OccurredAt >= start, Amount = line.Credit - line.Debit
            });
        if (teacherId.HasValue) attributed = attributed.Where(x => x.TeacherId == teacherId);
        var rows = await attributed.GroupBy(x => new { x.TeacherId, x.Role, x.SourceType, x.InPeriod })
            .Select(group => new SummaryAmount(group.Key.TeacherId!.Value, group.Key.Role,
                group.Key.SourceType, group.Key.InPeriod, group.Sum(x => x.Amount))).ToListAsync(ct);
        var ids = rows.Select(x => x.TeacherId).ToArray();
        var names = await db.TeacherProfiles.AsNoTracking()
            .Where(x => teacherId.HasValue ? x.Id == teacherId : ids.Contains(x.Id))
            .Select(x => new { x.Id, x.User.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var byTeacher = rows.ToLookup(x => x.TeacherId);
        return names.Select(teacher => Summarize(teacher.Key, teacher.Value, byTeacher[teacher.Key]))
            .OrderByDescending(x => x.Outstanding).ToArray();
    }

    private static TeacherFinancialSummaryDto Summarize(Guid teacherId, string name, IEnumerable<SummaryAmount> amounts)
    {
        var all = amounts.ToList();
        var period = all.Where(x => x.InPeriod).ToList();
        var payable = period.Where(x => x.Role == FinancialAccountRole.TeacherPayable).ToList();
        var paid = -payable.Where(x => x.SourceType is "TeacherSettlement" or "TeacherPayout" or "Payroll").Sum(x => x.Amount);
        var teacherShare = payable.Sum(x => x.Amount) + paid;
        var platformRefunds = -period.Where(x => x.Role == FinancialAccountRole.Refunds).Sum(x => x.Amount);
        var refunds = platformRefunds - payable.Where(x => x.SourceType == "PlatformRefund").Sum(x => x.Amount);
        var platformShare = period.Where(x => x.Role == FinancialAccountRole.PlatformRevenue).Sum(x => x.Amount) - platformRefunds;
        var outstanding = all.Where(x => x.Role == FinancialAccountRole.TeacherPayable).Sum(x => x.Amount);
        return new(teacherId, name, teacherShare + platformShare + refunds, platformShare, teacherShare, refunds, paid, outstanding);
    }

    private sealed record SummaryAmount(Guid TeacherId, FinancialAccountRole Role, string SourceType, bool InPeriod, decimal Amount);
}
