using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record PurchaseContentCommand(Guid StudentId, CodeType ContentType, Guid ContentId) : IRequest<ApiResponse<bool>>;

public class PurchaseContentCommandHandler : IRequestHandler<PurchaseContentCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public PurchaseContentCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(PurchaseContentCommand request, CancellationToken ct)
    {
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

        // 3. Get Student Balance
        var balance = await _db.StudentBalances
            .FirstOrDefaultAsync(b => b.UserId == request.StudentId, ct);
            
        if (balance == null)
        {
            balance = new StudentBalance { Id = Guid.NewGuid(), UserId = request.StudentId, CurrentBalance = 0 };
            _db.StudentBalances.Add(balance);
        }

        if (balance.CurrentBalance < price)
        {
            return ApiResponse<bool>.Fail($"رصيدك الحالي ({balance.CurrentBalance} ج.م) لا يكفي لشراء {contentName} بسعر ({price} ج.م)");
        }

        // 4. Perform Transaction (Atomic by SaveChanges)
        balance.CurrentBalance -= price;
        balance.UpdatedAt = DateTime.UtcNow;

        var transaction = new BalanceTransaction
        {
            Id = Guid.NewGuid(),
            StudentBalanceId = balance.Id,
            Amount = -price,
            BalanceAfter = balance.CurrentBalance,
            TransactionType = "ContentPurchase",
            ReferenceId = request.ContentId,
            Description = $"شراء {contentName} ({request.ContentType})",
            CreatedAt = DateTime.UtcNow
        };
        _db.BalanceTransactions.Add(transaction);

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

        return ApiResponse<bool>.Ok(true, "تم الشراء بنجاح");
    }
}
