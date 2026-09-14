using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class DiscountEngine : IDiscountEngine
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public DiscountEngine(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public Task<DiscountCalculationResult> PreviewAsync(Guid studentId, SalesTargetContext target, DiscountInput input, Guid operationId, CancellationToken cancellationToken = default)
        => CalculateAsync(studentId, target, input, operationId, commit: false, cancellationToken);

    public Task<DiscountCalculationResult> CommitAsync(Guid studentId, SalesTargetContext target, DiscountInput input, Guid operationId, CancellationToken cancellationToken = default)
        => CalculateAsync(studentId, target, input, operationId, commit: true, cancellationToken);

    private async Task<DiscountCalculationResult> CalculateAsync(Guid studentId, SalesTargetContext target, DiscountInput input, Guid operationId, bool commit, CancellationToken ct)
    {
        if (target.TargetId is null)
            return Fail(operationId, target, "هدف البيع غير محدد.");

        if (!target.IsSaleEligible)
            return Fail(operationId, target, "المحتوى غير متاح للبيع حالياً.");

        if (_academicScope != null)
        {
            var ownerType = ResolveAcademicOwnerType(target.TargetType);
            if (ownerType.HasValue)
            {
                var academicResult = await _academicScope.ValidateStudentCanUseTargetAsync(
                    ownerType.Value,
                    target.TargetId.Value,
                    studentId,
                    ct);
                if (!academicResult.IsEligible)
                    return Fail(operationId, target, academicResult.Message ?? "هذا المحتوى غير متاح لنطاقك الدراسي الحالي.");
            }
        }

        var now = DateTime.UtcNow;
        var gross = Math.Max(0, target.Price);
        var lines = new List<DiscountLine>();
        var couponTotal = 0m;
        var printableTotal = 0m;
        var remaining = gross;
        var couponCodes = (input.CouponCodes ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeCode)
            .Distinct()
            .ToList();
        var printableCodes = (input.PrintableCodes ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (couponCodes.Count == 0 && printableCodes.Count == 0)
            return Success(operationId, target, gross, 0, 0, lines);

        var defaultPolicy = await _db.DiscountStackingPolicies
            .Where(x => x.IsActive && x.IsDefault)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var stackingMode = defaultPolicy?.Mode ?? StackingMode.SingleOnly;
        var maxDiscount = ResolveMaxDiscount(gross, defaultPolicy);
        if (stackingMode == StackingMode.SingleOnly && couponCodes.Count + printableCodes.Count > 1)
            return Fail(operationId, target, "سياسة الخصم الحالية تسمح بكود واحد فقط.");

        foreach (var normalizedCode in couponCodes)
        {
            var coupon = await _db.SalesCoupons.FirstOrDefaultAsync(x => x.NormalizedCode == normalizedCode, ct);
            if (coupon == null)
                return Fail(operationId, target, $"كوبون الخصم {normalizedCode} غير موجود.");

            var validation = await ValidateCouponAsync(coupon, studentId, target, now, ct);
            if (validation != null)
                return Fail(operationId, target, validation);

            var amount = ClampDiscount(CalculateAmount(coupon.DiscountType, coupon.DiscountValue, gross), remaining, maxDiscount - couponTotal - printableTotal);
            if (amount <= 0) continue;

            remaining -= amount;
            couponTotal += amount;
            lines.Add(new DiscountLine("coupon", coupon.Id, coupon.Code, amount, coupon.Name));

            if (commit)
            {
                coupon.UsedCount++;
                coupon.UpdatedAt = now;
                _db.SalesCouponUsages.Add(new SalesCouponUsage
                {
                    CouponId = coupon.Id,
                    StudentId = studentId,
                    PurchaseOperationId = operationId,
                    TargetType = target.TargetType,
                    TargetId = target.TargetId.Value,
                    GrossAmount = gross,
                    DiscountAmount = amount
                });
            }
        }

        foreach (var rawCode in printableCodes)
        {
            var hash = HashCode(rawCode);
            var printableCode = await _db.PrintableSalesCodes
                .Include(x => x.Batch)
                .FirstOrDefaultAsync(x => x.CodeHash == hash, ct);
            if (printableCode == null)
                return Fail(operationId, target, "الكود المطبوع غير موجود.");

            var validation = ValidatePrintable(printableCode, target, now);
            if (validation != null)
                return Fail(operationId, target, validation);

            if (printableCode.Batch.Behavior == PrintableCodeBehavior.DirectAccess)
                continue;

            var amount = printableCode.Batch.Behavior == PrintableCodeBehavior.PromotionalCredit
                ? printableCode.Batch.CreditAmount ?? 0
                : CalculateAmount(printableCode.Batch.DiscountType ?? DiscountType.FixedAmount, printableCode.Batch.DiscountValue ?? 0, gross);
            amount = ClampDiscount(amount, remaining, maxDiscount - couponTotal - printableTotal);
            if (amount <= 0) continue;

            remaining -= amount;
            printableTotal += amount;
            lines.Add(new DiscountLine("printableCode", printableCode.Id, printableCode.CodePlaintext ?? rawCode, amount, printableCode.SerialNumber.ToString()));

            if (commit)
            {
                printableCode.UsedCount++;
                printableCode.Status = printableCode.UsedCount >= printableCode.UsageLimit ? SalesStatus.Consumed : SalesStatus.Active;
                printableCode.ConsumedByUserId = studentId;
                printableCode.ConsumedAt ??= now;
                printableCode.Batch.UsedCount++;
                _db.PrintableCodeRedemptions.Add(new PrintableCodeRedemption
                {
                    PrintableCodeId = printableCode.Id,
                    StudentId = studentId,
                    RequestId = operationId,
                    PurchaseOperationId = operationId,
                    TargetType = target.TargetType,
                    TargetId = target.TargetId.Value,
                    AppliedAmount = amount
                });
            }
        }

        return Success(operationId, target, gross, couponTotal, printableTotal, lines);
    }

    private async Task<string?> ValidateCouponAsync(SalesCoupon coupon, Guid studentId, SalesTargetContext target, DateTime now, CancellationToken ct)
    {
        if (coupon.Status != SalesStatus.Active)
            return "الكوبون غير مفعل.";
        if (coupon.StartsAt.HasValue && coupon.StartsAt.Value > now)
            return "الكوبون لم يبدأ بعد.";
        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value <= now)
            return "انتهت صلاحية الكوبون.";
        if (coupon.GlobalUsageLimit.HasValue && coupon.UsedCount >= coupon.GlobalUsageLimit.Value)
            return "تم استهلاك حد استخدام الكوبون.";
        if (coupon.PerStudentUsageLimit.HasValue)
        {
            var used = await _db.SalesCouponUsages.CountAsync(x => x.CouponId == coupon.Id && x.StudentId == studentId, ct);
            if (used >= coupon.PerStudentUsageLimit.Value)
                return $"تم استخدام الكوبون {coupon.Code} لهذا الطالب قبل ذلك.";
        }
        return IsTargetMatch(coupon.TargetType, coupon.TargetId, coupon.TeacherId, target) ? null : "الكوبون غير صالح لهذا المحتوى.";
    }

    private static string? ValidatePrintable(PrintableSalesCode code, SalesTargetContext target, DateTime now)
    {
        if (code.Status is not SalesStatus.Active)
            return "الكود المطبوع غير مفعل.";
        if (code.UsedCount >= code.UsageLimit)
            return "تم استهلاك الكود المطبوع.";
        if (code.Batch.Status is not SalesStatus.Active)
            return "دفعة الأكواد غير مفعلة.";
        if (code.Batch.StartsAt.HasValue && code.Batch.StartsAt.Value > now)
            return "الكود لم يبدأ بعد.";
        if (code.Batch.ExpiresAt.HasValue && code.Batch.ExpiresAt.Value <= now)
            return "انتهت صلاحية الكود.";
        return IsTargetMatch(code.Batch.TargetType, code.Batch.TargetId, code.Batch.TeacherId, target) ? null : "الكود غير صالح لهذا المحتوى.";
    }

    private static bool IsTargetMatch(SalesTargetType sourceType, Guid? sourceId, Guid? sourceTeacherId, SalesTargetContext target)
    {
        return sourceType == SalesTargetType.Platform
            || (sourceType == SalesTargetType.Teacher && sourceTeacherId.HasValue && sourceTeacherId == target.TeacherId)
            || (sourceType == target.TargetType && (!sourceId.HasValue || sourceId == target.TargetId))
            || (sourceType == SalesTargetType.VideoType && sourceId.HasValue && sourceId == target.VideoTypeId);
    }

    private static StudentFacingScopeOwnerType? ResolveAcademicOwnerType(SalesTargetType targetType)
    {
        return targetType switch
        {
            SalesTargetType.Package => StudentFacingScopeOwnerType.Package,
            SalesTargetType.Term => StudentFacingScopeOwnerType.Term,
            SalesTargetType.ContentSection => StudentFacingScopeOwnerType.ContentSection,
            SalesTargetType.Lesson => StudentFacingScopeOwnerType.Lesson,
            SalesTargetType.SpecificVideo => StudentFacingScopeOwnerType.LessonVideo,
            SalesTargetType.PublicExam => StudentFacingScopeOwnerType.PublicExamProduct,
            SalesTargetType.Teacher => StudentFacingScopeOwnerType.Teacher,
            _ => null
        };
    }

    private static decimal ResolveMaxDiscount(decimal gross, DiscountStackingPolicy? policy)
    {
        var max = gross;
        if (policy?.MaxDiscountAmount is decimal amount)
            max = Math.Min(max, amount);
        if (policy?.MaxDiscountPercentage is decimal percentage)
            max = Math.Min(max, gross * percentage / 100m);
        return Math.Max(0, max);
    }

    private static decimal CalculateAmount(DiscountType type, decimal value, decimal gross)
        => type == DiscountType.Percentage ? gross * value / 100m : value;

    private static decimal ClampDiscount(decimal amount, decimal remaining, decimal remainingCap)
        => Math.Max(0, Math.Min(Math.Min(amount, remaining), Math.Max(0, remainingCap)));

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeCode(code)));
        return Convert.ToHexString(bytes);
    }

    private static DiscountCalculationResult Success(Guid operationId, SalesTargetContext target, decimal gross, decimal coupon, decimal printable, IReadOnlyList<DiscountLine> lines)
        => new(true, null, operationId, target.TargetType, target.TargetId!.Value, gross, coupon, printable, coupon + printable, lines);

    private static DiscountCalculationResult Fail(Guid operationId, SalesTargetContext target, string error)
        => new(false, error, operationId, target.TargetType, target.TargetId ?? Guid.Empty, target.Price, 0, 0, 0, Array.Empty<DiscountLine>());
}
