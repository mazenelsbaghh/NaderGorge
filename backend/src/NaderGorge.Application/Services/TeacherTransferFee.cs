using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed record TeacherTransferQuote(decimal TeacherAmount, decimal PlatformShareBasis,
    decimal FeeRate, decimal TransferFee, decimal NetTransferAmount);

public static class TeacherTransferFee
{
    public const string Description = "عمولة تحويل فودافون كاش";
    public const decimal Rate = 1.5m;

    public static IQueryable<TeacherSettlementLine> PaidLines(IAppDbContext db) => db.TeacherSettlementLines.AsNoTracking()
        .Where(x => x.AllocationId == null && x.AdjustmentId == null && x.Amount < 0m
            && x.DescriptionSnapshot.StartsWith(Description) && x.TeacherSettlement.Status == TeacherSettlementStatus.Paid);

    public static TeacherTransferQuote Quote(string paymentMethod, IReadOnlyCollection<TeacherFinancialAllocation> allocations,
        decimal teacherAmount)
    {
        var method = string.Concat(paymentMethod.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        if (teacherAmount <= 0m || method is not ("vodafonecash" or "vfcash" or "فودافونكاش" or "فودفونكاش"))
            return new(teacherAmount, 0m, 0m, 0m, teacherAmount);

        // Refunds and debt offsets reduce the platform basis attributable to this payment.
        var gross = allocations.Sum(x => Math.Max(0m, x.TeacherShareAmount - x.ReversedAmount));
        var platform = allocations.Where(x => x.TeacherShareAmount > 0m).Sum(x =>
            Math.Max(0m, x.PlatformShareAmount) * Math.Max(0m, x.TeacherShareAmount - x.ReversedAmount) / x.TeacherShareAmount);
        var basis = gross == 0m ? 0m : decimal.Round(platform * teacherAmount / gross, 2, MidpointRounding.AwayFromZero);
        var fee = decimal.Round(basis * Rate / 100m, 2, MidpointRounding.AwayFromZero);
        return new(teacherAmount, basis, Rate, fee, teacherAmount - fee);
    }
}
