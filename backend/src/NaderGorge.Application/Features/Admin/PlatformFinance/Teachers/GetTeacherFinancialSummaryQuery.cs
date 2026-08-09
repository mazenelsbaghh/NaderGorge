using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;

public sealed record TeacherFinancialSummaryDto(Guid TeacherId, string TeacherName, decimal GrossSales, decimal PlatformShare, decimal TeacherShare, decimal Refunds, decimal Paid, decimal Outstanding);

public sealed class GetTeacherFinancialSummaryQuery(IAppDbContext db)
{
    public async Task<TeacherFinancialSummaryDto?> GetAsync(Guid teacherId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (start, end) = CairoTime.GetRollingMonthRangeUtc(from, to);
        var rows = await db.JournalLines.AsNoTracking()
            .Where(line => line.TeacherId == teacherId && line.JournalEntry.Status == JournalEntryStatus.Posted && line.JournalEntry.OccurredAt >= start && line.JournalEntry.OccurredAt < end)
            .Select(line => new { line.FinancialAccount.Role, line.Debit, line.Credit, line.JournalEntry.SourceType })
            .ToListAsync(ct);
        var profile = await db.TeacherProfiles.AsNoTracking().Where(x => x.Id == teacherId).Select(x => new { x.User.FullName }).SingleOrDefaultAsync(ct);
        if (profile is null) return null;
        var teacherShare = rows.Where(x => x.Role == FinancialAccountRole.TeacherPayable).Sum(x => x.Credit - x.Debit);
        var refunds = rows.Where(x => x.Role == FinancialAccountRole.Refunds).Sum(x => x.Debit - x.Credit);
        var paid = rows.Where(x => x.SourceType is "TeacherSettlement" or "Payroll").Sum(x => x.Debit - x.Credit);
        return new(teacherId, profile.FullName, teacherShare + refunds, rows.Where(x => x.Role == FinancialAccountRole.PlatformRevenue).Sum(x => x.Credit - x.Debit), teacherShare, refunds, paid, teacherShare - paid);
    }
}
