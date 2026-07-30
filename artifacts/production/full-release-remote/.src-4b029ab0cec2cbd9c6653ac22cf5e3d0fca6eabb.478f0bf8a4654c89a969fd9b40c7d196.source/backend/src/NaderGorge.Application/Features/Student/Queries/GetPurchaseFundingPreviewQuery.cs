using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public sealed record GetPurchaseFundingPreviewQuery(Guid StudentId, CodeType ContentType, Guid ContentId, IReadOnlyList<string>? CouponCodes = null, IReadOnlyList<string>? PrintableCodes = null)
    : IRequest<ApiResponse<PurchaseFundingPreviewDto>>;

public sealed record PurchaseFundingPreviewDto(
    decimal Price,
    decimal CouponDiscountAmount,
    decimal PrintableCodeDiscountAmount,
    decimal DiscountedPrice,
    decimal EligiblePromotionalAmount,
    decimal PromotionalAmountToUse,
    decimal PaidAmountToUse,
    decimal CurrentPaidBalance,
    bool IsSufficient);

public sealed class GetPurchaseFundingPreviewQueryHandler
    : IRequestHandler<GetPurchaseFundingPreviewQuery, ApiResponse<PurchaseFundingPreviewDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPromotionalBalanceService _promotional;
    private readonly ISalesTargetResolver _targetResolver;
    private readonly IDiscountEngine _discountEngine;
    private readonly IAcademicScopeService? _academicScope;

    public GetPurchaseFundingPreviewQueryHandler(
        IAppDbContext db,
        IPromotionalBalanceService promotional,
        ISalesTargetResolver targetResolver,
        IDiscountEngine discountEngine,
        IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _promotional = promotional;
        _targetResolver = targetResolver;
        _discountEngine = discountEngine;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<PurchaseFundingPreviewDto>> Handle(GetPurchaseFundingPreviewQuery request, CancellationToken ct)
    {
        var target = await _targetResolver.ResolveFromCodeTypeAsync(request.ContentType, request.ContentId, ct);
        if (target == null)
            return ApiResponse<PurchaseFundingPreviewDto>.Fail("المحتوى غير موجود أو غير مدعوم للشراء.");

        if (_academicScope != null)
        {
            var (ownerType, ownerId) = ResolveAcademicOwner(request.ContentType, request.ContentId);
            if (ownerType.HasValue)
            {
                var academicResult = await _academicScope.ValidateStudentCanUseTargetAsync(
                    ownerType.Value,
                    ownerId,
                    request.StudentId,
                    ct);
                if (!academicResult.IsEligible)
                {
                    return ApiResponse<PurchaseFundingPreviewDto>.Fail(
                        academicResult.Message ?? "هذا المحتوى غير متاح لنطاقك الدراسي الحالي.",
                        new List<string> { academicResult.ErrorCode ?? "ACADEMIC_SCOPE_DENIED" });
                }
            }
        }

        var price = target.Price;
        var discount = await _discountEngine.PreviewAsync(
            request.StudentId,
            target,
            new DiscountInput(request.CouponCodes ?? Array.Empty<string>(), request.PrintableCodes ?? Array.Empty<string>()),
            Guid.NewGuid(),
            ct);
        if (!discount.Success)
            return ApiResponse<PurchaseFundingPreviewDto>.Fail(discount.Error ?? "تعذر حساب الخصم.");

        var discountedPrice = Math.Max(0, price - discount.TotalDiscountAmount);
        var teacherId = await _promotional.ResolveTeacherIdAsync(request.ContentType, request.ContentId, ct);
        var promotionalAvailable = await _promotional.GetEligibleAmountAsync(request.StudentId, teacherId, ct);
        var paidBalance = await _db.StudentBalances
            .Where(x => x.UserId == request.StudentId)
            .Select(x => x.CurrentBalance)
            .FirstOrDefaultAsync(ct);
        var promotionalToUse = Math.Min(discountedPrice, promotionalAvailable);
        var paidToUse = discountedPrice - promotionalToUse;

        return ApiResponse<PurchaseFundingPreviewDto>.Ok(new PurchaseFundingPreviewDto(
            price,
            discount.CouponDiscountAmount,
            discount.PrintableCodeDiscountAmount,
            discountedPrice,
            promotionalAvailable,
            promotionalToUse,
            paidToUse,
            paidBalance,
            paidBalance >= paidToUse));
    }

    private static (StudentFacingScopeOwnerType? OwnerType, Guid OwnerId) ResolveAcademicOwner(CodeType contentType, Guid contentId)
    {
        return contentType switch
        {
            CodeType.Package => (StudentFacingScopeOwnerType.Package, contentId),
            CodeType.Term => (StudentFacingScopeOwnerType.Term, contentId),
            CodeType.Month => (StudentFacingScopeOwnerType.ContentSection, contentId),
            CodeType.Lesson => (StudentFacingScopeOwnerType.Lesson, contentId),
            CodeType.Exam => (StudentFacingScopeOwnerType.PublicExamProduct, contentId),
            _ => (null, contentId)
        };
    }
}
