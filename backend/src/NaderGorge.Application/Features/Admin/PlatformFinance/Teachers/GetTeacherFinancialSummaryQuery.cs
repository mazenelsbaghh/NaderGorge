using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Services;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;

public sealed record TeacherFinancialSummaryDto(Guid TeacherId, string TeacherName, decimal GrossSales, decimal PlatformShare, decimal TeacherShare, decimal Refunds, decimal Paid, decimal Outstanding, decimal Adjustments = 0m, TeacherFinanceAccountSnapshot? Account = null);

public sealed class GetTeacherFinancialSummaryQuery(IAppDbContext db)
{
    public async Task<TeacherFinancialSummaryDto?> GetAsync(Guid teacherId, DateTime? from, DateTime? to, CancellationToken ct) =>
        (await ReadAsync(teacherId, from, to, ct)).SingleOrDefault();

    public Task<IReadOnlyList<TeacherFinancialSummaryDto>> GetAllAsync(DateTime? from, DateTime? to, CancellationToken ct) =>
        ReadAsync(null, from, to, ct);

    private async Task<IReadOnlyList<TeacherFinancialSummaryDto>> ReadAsync(Guid? teacherId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (start, end) = FinancialLedgerQuery.Period(from, to);
        // Older single-teacher entries omitted the teacher dimension on platform lines.
        // Shared entries without a dimension cannot safely be assigned to either teacher.
        var attributed = new FinancialLedgerQuery(db).Lines
            .Where(line => line.JournalEntry.OccurredAt < end
                && (line.TeacherId != null ||
                    ((line.FinancialAccount.Role == FinancialAccountRole.PlatformRevenue || line.FinancialAccount.Role == FinancialAccountRole.Refunds)
                        && line.JournalEntry.Lines.Where(x => x.TeacherId != null).Select(x => x.TeacherId).Distinct().Count() == 1)))
            .Select(line => new
            {
                TeacherId = line.TeacherId ?? line.JournalEntry.Lines.Where(x => x.TeacherId != null).Select(x => x.TeacherId).FirstOrDefault(),
                line.FinancialAccount.Role,
                SourceType = line.JournalEntry.PostingKind == "Reversal"
                    ? db.JournalEntries.Where(original => original.Id == (line.JournalEntry.ReversalOfId ?? line.JournalEntry.SourceId))
                        .Select(original => original.SourceType).FirstOrDefault() ?? line.JournalEntry.SourceType
                    : line.JournalEntry.SourceType,
                InPeriod = line.JournalEntry.OccurredAt >= start, Amount = line.Credit - line.Debit
            });
        if (teacherId.HasValue) attributed = attributed.Where(x => x.TeacherId == teacherId);
        var rows = await attributed.GroupBy(x => new { x.TeacherId, x.Role, x.SourceType, x.InPeriod })
            .Select(group => new SummaryAmount(group.Key.TeacherId!.Value, group.Key.Role,
                group.Key.SourceType, group.Key.InPeriod, group.Sum(x => x.Amount))).ToListAsync(ct);
        var names = await db.TeacherProfiles.AsNoTracking()
            .Where(x => !teacherId.HasValue || x.Id == teacherId)
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
        var paid = -payable.Where(x => x.SourceType is "TeacherSettlement" or "TeacherPayout" or "Payroll" or "TeacherRetained").Sum(x => x.Amount);
        var teacherSales = payable.Where(x => IsSale(x.SourceType)).Sum(x => x.Amount);
        var teacherRefunds = -payable.Where(x => x.SourceType == "PlatformRefund").Sum(x => x.Amount);
        var teacherShare = teacherSales - teacherRefunds;
        var adjustments = payable.Sum(x => x.Amount) + paid - teacherShare;
        var platformRefunds = -period.Where(x => x.Role == FinancialAccountRole.Refunds).Sum(x => x.Amount);
        var refunds = platformRefunds + teacherRefunds;
        var platformShare = period.Where(x => x.Role == FinancialAccountRole.PlatformRevenue).Sum(x => x.Amount) - platformRefunds;
        var outstanding = all.Where(x => x.Role == FinancialAccountRole.TeacherPayable).Sum(x => x.Amount);
        return new(teacherId, name, teacherShare + platformShare + refunds, platformShare, teacherShare, refunds, paid, outstanding, adjustments);
    }

    private static bool IsSale(string sourceType) => sourceType is "Purchase" or "DirectSale" or "CodeSale"
        or "PublicExamSale" or "SharedPackageSale" or "TeacherFundingCorrection";

    private sealed record SummaryAmount(Guid TeacherId, FinancialAccountRole Role, string SourceType, bool InPeriod, decimal Amount);
}
