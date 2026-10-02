using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

internal sealed class TeacherReportFundingReader(IAppDbContext db)
{
    public async Task<TeacherReportFunding[]> ReadAsync(Guid teacherId, DateTime end, CancellationToken ct)
    {
        var credits = await db.PromotionalBalanceAllocations.AsNoTracking().Include(x => x.Student)
            .Include(x => x.GiftRecipient).ThenInclude(x => x.GiftIssuance)
            .Where(x => x.TeacherId == teacherId && x.CreatedAt < end).ToListAsync(ct);
        var usages = await db.PromotionalBalanceUsages.AsNoTracking().Where(x => x.Allocation.TeacherId == teacherId && x.CreatedAt < end)
            .Select(x => new { x.AllocationId, x.Amount }).ToListAsync(ct);
        var used = usages.GroupBy(x => x.AllocationId)
            .ToDictionary(g => g.Key, g => new { Amount = g.Sum(x => x.Amount), Count = g.Count() });
        var codes = await db.AccessCodes.AsNoTracking().Where(x => x.CodeGroup.TeacherId == teacherId
            && x.CodeGroup.CodeType == CodeType.Balance && x.IsConsumed && x.ConsumedAt < end && x.ConsumedByUserId.HasValue)
            .Select(x => new { StudentId = x.ConsumedByUserId!.Value, At = x.ConsumedAt!.Value, Amount = x.CodeGroup.BalanceAmount }).ToListAsync(ct);
        return credits.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(credit =>
        {
            var usage = used.GetValueOrDefault(credit.Id);
            var exhausted = (credit.ExpiresAt.HasValue && credit.ExpiresAt < end)
                || (credit.GiftRecipient.RevokedAt.HasValue && credit.GiftRecipient.RevokedAt < end)
                || (credit.MaxPurchaseCount.HasValue && (usage?.Count ?? 0) >= credit.MaxPurchaseCount);
            var isCode = credit.GiftRecipient.GiftIssuance.Reason.StartsWith("Teacher scoped balance code:", StringComparison.Ordinal);
            // Owner-confirmed policy: scoped balance codes are paid to the teacher; match actual redemption evidence.
            var paidCode = isCode && codes.Count(x => x.StudentId == credit.StudentId && x.Amount == credit.OriginalAmount
                && Math.Abs((x.At - credit.CreatedAt).TotalSeconds) < 2) == 1;
            bool? paid = credit.GiftRecipient.OutcomeCode == "DIGITAL_RECHARGE" || paidCode ? true
                : isCode || credit.GiftRecipient.OutcomeCode == "ADMIN_ADJUSTMENT" ? null : false;
            var source = paidCode ? "كود مدفوع قبضه المدرس" : credit.GiftRecipient.OutcomeCode == "DIGITAL_RECHARGE" ? "شحن مدفوع"
                : isCode ? "رصيد من كود (الدفع محتاج مراجعة)" : paid is null ? "إضافة رصيد محتاجة مراجعة" : "هدية أو رصيد مجاني";
            return new TeacherReportFunding(credit.StudentId, credit.Student.FullName, source, credit.OriginalAmount,
                usage?.Amount ?? 0, exhausted ? 0 : Math.Max(0, credit.OriginalAmount - (usage?.Amount ?? 0)), credit.CreatedAt, paid);
        }).ToArray();
    }
}
