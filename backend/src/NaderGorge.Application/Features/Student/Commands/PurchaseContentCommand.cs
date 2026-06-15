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
                case CodeType.Term:
                    var term = await _db.Terms.FirstOrDefaultAsync(t => t.Id == request.ContentId, ct);
                    if (term == null) return ApiResponse<bool>.Fail("الترم غير موجود");
                    price = term.Price;
                    contentName = term.Title;
                    break;
                case CodeType.Month:
                    var section = await _db.ContentSections.FirstOrDefaultAsync(s => s.Id == request.ContentId, ct);
                    if (section == null) return ApiResponse<bool>.Fail("القسم غير موجود");
                    price = section.Price;
                    contentName = section.Title;
                    break;
                case CodeType.Lesson:
                    var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == request.ContentId, ct);
                    if (lesson == null) return ApiResponse<bool>.Fail("الحصة غير موجودة");
                    price = lesson.Price;
                    contentName = lesson.Title;
                    break;
                default:
                    return ApiResponse<bool>.Fail("نوع المحتوى غير مدعوم للشراء.");
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
                var failEvent = new OutboxEvent
                {
                    Type = "PurchaseFailed",
                    TargetUserId = request.StudentId.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        studentId = request.StudentId,
                        contentType = request.ContentType.ToString(),
                        contentId = request.ContentId,
                        reason = "already_purchased"
                    })
                };
                _db.OutboxEvents.Add(failEvent);
                await _db.SaveChangesAsync(ct);
                return ApiResponse<bool>.Fail("تم شراء هذا المحتوى مسبقاً");
            }

            // 3. Check if student already has access from a HIGHER-LEVEL grant
            // Hierarchy: Package > Term > Section (Month) > Lesson
            // If they own the parent, they can't buy the child (it's already included)
            string? coveredBy = null;
            switch (request.ContentType)
            {
                case CodeType.Term:
                {
                    // Can't buy a Term if they already own its Package
                    var termCheck = await _db.Terms.FirstOrDefaultAsync(t => t.Id == request.ContentId, ct);
                    if (termCheck != null)
                    {
                        bool hasPackage = await _db.StudentAccessGrants.AnyAsync(g =>
                            g.UserId == request.StudentId && g.IsActive &&
                            g.GrantType == CodeType.Package && g.PackageId == termCheck.PackageId, ct);
                        if (hasPackage) coveredBy = "الباقة الكاملة (السنة)";
                    }
                    break;
                }
                case CodeType.Month:
                {
                    // Can't buy a Section if they own its Term or its Package
                    var sectionCheck = await _db.ContentSections
                        .Include(s => s.Term)
                        .FirstOrDefaultAsync(s => s.Id == request.ContentId, ct);
                    if (sectionCheck != null)
                    {
                        bool hasTerm = await _db.StudentAccessGrants.AnyAsync(g =>
                            g.UserId == request.StudentId && g.IsActive &&
                            g.GrantType == CodeType.Term && g.TermId == sectionCheck.TermId, ct);
                        if (hasTerm) { coveredBy = "الترم"; break; }

                        var sectionPackageId = sectionCheck.Term?.PackageId;
                        if (sectionPackageId != null)
                        {
                            bool hasPackage = await _db.StudentAccessGrants.AnyAsync(g =>
                                g.UserId == request.StudentId && g.IsActive &&
                                g.GrantType == CodeType.Package && g.PackageId == sectionPackageId, ct);
                            if (hasPackage) coveredBy = "الباقة الكاملة (السنة)";
                        }
                    }
                    break;
                }
                case CodeType.Lesson:
                {
                    // Can't buy a Lesson if they own its Section, Term, or Package
                    var lessonCheck = await _db.Lessons
                        .Include(l => l.ContentSection)
                        .ThenInclude(s => s.Term)
                        .FirstOrDefaultAsync(l => l.Id == request.ContentId, ct);
                    if (lessonCheck != null)
                    {
                        bool hasSection = await _db.StudentAccessGrants.AnyAsync(g =>
                            g.UserId == request.StudentId && g.IsActive &&
                            g.GrantType == CodeType.Month && g.ContentSectionId == lessonCheck.ContentSectionId, ct);
                        if (hasSection) { coveredBy = "القسم"; break; }

                        var lessonTermId = lessonCheck.ContentSection?.TermId;
                        if (lessonTermId != null)
                        {
                            bool hasTerm = await _db.StudentAccessGrants.AnyAsync(g =>
                                g.UserId == request.StudentId && g.IsActive &&
                                g.GrantType == CodeType.Term && g.TermId == lessonTermId, ct);
                            if (hasTerm) { coveredBy = "الترم"; break; }
                        }

                        var lessonPackageId = lessonCheck.ContentSection?.Term?.PackageId;
                        if (lessonPackageId != null)
                        {
                            bool hasPackage = await _db.StudentAccessGrants.AnyAsync(g =>
                                g.UserId == request.StudentId && g.IsActive &&
                                g.GrantType == CodeType.Package && g.PackageId == lessonPackageId, ct);
                            if (hasPackage) coveredBy = "الباقة الكاملة (السنة)";
                        }
                    }
                    break;
                }
            }

            if (coveredBy != null)
            {
                return ApiResponse<bool>.Fail($"أنت مشترك بالفعل في {coveredBy} — لا يمكن شراء {contentName} بشكل منفصل لأنها مغطاة بالاشتراك الحالي.");
            }

            if (price > 0)
            {
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
                    var failEvent = new OutboxEvent
                    {
                        Type = "PurchaseFailed",
                        TargetUserId = request.StudentId.ToString(),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            studentId = request.StudentId,
                            contentType = request.ContentType.ToString(),
                            contentId = request.ContentId,
                            reason = "insufficient_balance"
                        })
                    };
                    _db.OutboxEvents.Add(failEvent);
                    await _db.SaveChangesAsync(ct);
                    return ApiResponse<bool>.Fail($"رصيدك الحالي لا يكفي لشراء {contentName} بسعر ({price} ج.م)");
                }
            }

            // 5. Grant Access
            var grant = new StudentAccessGrant
            {
                Id = Guid.NewGuid(),
                UserId = request.StudentId,
                GrantType = request.ContentType,
                GrantedAt = DateTime.UtcNow,
                IsActive = true
            };

            switch (request.ContentType)
            {
                case CodeType.Package:
                    grant.PackageId = request.ContentId;
                    break;
                case CodeType.Term:
                {
                    grant.TermId = request.ContentId;
                    var termForGrant = await _db.Terms.FirstOrDefaultAsync(t => t.Id == request.ContentId, ct);
                    if (termForGrant != null) grant.PackageId = termForGrant.PackageId;
                    break;
                }
                case CodeType.Month:
                {
                    grant.ContentSectionId = request.ContentId;
                    var sectionForGrant = await _db.ContentSections
                        .Include(s => s.Term)
                        .FirstOrDefaultAsync(s => s.Id == request.ContentId, ct);
                    if (sectionForGrant != null)
                    {
                        grant.TermId = sectionForGrant.TermId;
                        grant.PackageId = sectionForGrant.Term?.PackageId;
                    }
                    break;
                }
                case CodeType.Lesson:
                {
                    grant.LessonId = request.ContentId;
                    var lessonForGrant = await _db.Lessons
                        .Include(l => l.ContentSection)
                        .ThenInclude(s => s.Term)
                        .FirstOrDefaultAsync(l => l.Id == request.ContentId, ct);
                    if (lessonForGrant != null)
                    {
                        grant.ContentSectionId = lessonForGrant.ContentSectionId;
                        grant.TermId = lessonForGrant.ContentSection?.TermId;
                        grant.PackageId = lessonForGrant.ContentSection?.Term?.PackageId;
                    }
                    break;
                }
            }

            _db.StudentAccessGrants.Add(grant);

            if (request.ContentType == CodeType.Package)
            {
                var packageAccessGrantedEvent = new OutboxEvent
                {
                    Type = "PackageAccessGranted",
                    TargetUserId = request.StudentId.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        userId = request.StudentId,
                        packageId = request.ContentId
                    })
                };
                _db.OutboxEvents.Add(packageAccessGrantedEvent);
            }

            var purchaseCompletedEvent = new OutboxEvent
            {
                Type = "PurchaseCompleted",
                TargetUserId = request.StudentId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    studentId = request.StudentId,
                    contentType = request.ContentType.ToString(),
                    contentId = request.ContentId,
                    price = price
                })
            };
            _db.OutboxEvents.Add(purchaseCompletedEvent);

            await _db.SaveChangesAsync(ct);

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
            || ex.Message.Contains("transaction is aborted", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("25P02", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException != null && IsConcurrencyFailure(ex.InnerException));
    }
}
