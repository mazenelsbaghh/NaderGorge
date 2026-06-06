using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record PurchaseContentCommand(Guid StudentId, CodeType ContentType, Guid ContentId) : IRequest<ApiResponse<bool>>;

public class PurchaseContentCommandHandler : IRequestHandler<PurchaseContentCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly BalanceService _balanceService;

    public PurchaseContentCommandHandler(IAppDbContext db, BalanceService balanceService)
    {
        _db = db;
        _balanceService = balanceService;
    }

    public async Task<ApiResponse<bool>> Handle(PurchaseContentCommand request, CancellationToken ct)
    {
        try
        {
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        // 1. Validate content exists and get its price
        decimal price = 0;
        string contentName = "";

        switch (request.ContentType)
        {
            case CodeType.Package:
                var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == request.ContentId, ct);
                if (pkg == null) return ApiResponse<bool>.Fail("الباقة غير موجودة");
                price = pkg.Price;
                contentName = pkg.Name;
                break;
            default:
                return ApiResponse<bool>.Fail("شراء الأجزاء الفردية غير متاح بالرصيد حالياً، يمكنك استخدام كود شحن مخصص.");
        }

        // 2. Check if already purchased
        bool alreadyPurchased = false;
        switch (request.ContentType)
        {
            case CodeType.Package:
                alreadyPurchased = await _db.StudentAccessGrants.AnyAsync(g => g.UserId == request.StudentId && g.GrantType == request.ContentType && g.PackageId == request.ContentId && g.IsActive, ct);
                break;
            case CodeType.Term:
                alreadyPurchased = await _db.StudentAccessGrants.AnyAsync(g => g.UserId == request.StudentId && g.GrantType == request.ContentType && g.TermId == request.ContentId && g.IsActive, ct);
                break;
            case CodeType.Month:
                alreadyPurchased = await _db.StudentAccessGrants.AnyAsync(g => g.UserId == request.StudentId && g.GrantType == request.ContentType && g.ContentSectionId == request.ContentId && g.IsActive, ct);
                break;
            case CodeType.Lesson:
                alreadyPurchased = await _db.StudentAccessGrants.AnyAsync(g => g.UserId == request.StudentId && g.GrantType == request.ContentType && g.LessonId == request.ContentId && g.IsActive, ct);
                break;
        }

        if (alreadyPurchased)
        {
            return ApiResponse<bool>.Fail("تم شراء هذا المحتوى مسبقاً");
        }

        try
        {
            await _balanceService.DeductBalance(
                request.StudentId,
                price,
                $"شراء {contentName} ({request.ContentType})",
                request.ContentId,
                ct);
        }
        catch (InvalidOperationException)
        {
            return ApiResponse<bool>.Fail($"رصيدك الحالي لا يكفي لشراء {contentName} بسعر ({price} ج.م)");
        }

        // 5. Grant Access
        var grant = new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = request.StudentId,
            GrantType = request.ContentType,
            GrantedAt = DateTime.UtcNow,
            IsActive = true
            // If subscription expires, set ExpiresAt based on content config
        };

        switch (request.ContentType)
        {
            case CodeType.Package: grant.PackageId = request.ContentId; break;
            case CodeType.Term: grant.TermId = request.ContentId; break;
            case CodeType.Month: grant.ContentSectionId = request.ContentId; break;
            case CodeType.Lesson: grant.LessonId = request.ContentId; break;
        }

        _db.StudentAccessGrants.Add(grant);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ApiResponse<bool>.Ok(true, "تم الشراء بنجاح");
        }
        catch (Exception ex) when (IsConcurrencyFailure(ex))
        {
            return ApiResponse<bool>.Fail("تم تنفيذ عملية متزامنة قبل هذه المحاولة. راجع الرصيد وحاول مرة أخرى.");
        }
    }

    private static bool IsConcurrencyFailure(Exception ex)
    {
        return ex.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("concurrent update", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException != null && IsConcurrencyFailure(ex.InnerException));
    }
}
