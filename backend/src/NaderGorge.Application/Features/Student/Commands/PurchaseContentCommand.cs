using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record PurchaseContentCommand(Guid StudentId, CodeType ContentType, Guid ContentId, IReadOnlyList<string>? CouponCodes = null, IReadOnlyList<string>? PrintableCodes = null) : IRequest<ApiResponse<bool>>;

public class PurchaseContentCommandHandler : IRequestHandler<PurchaseContentCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly BalanceService _balanceService;
    private readonly IPromotionalBalanceService _promotionalBalance;
    private readonly ISalesTargetResolver _targetResolver;
    private readonly IDiscountEngine _discountEngine;
    private readonly TeacherAccountingService _teacherAccounting;
    private readonly IAcademicScopeService? _academicScope;

    public PurchaseContentCommandHandler(
        IAppDbContext db,
        BalanceService balanceService,
        IPromotionalBalanceService promotionalBalance,
        ISalesTargetResolver targetResolver,
        IDiscountEngine discountEngine,
        TeacherAccountingService? teacherAccounting = null,
        IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _balanceService = balanceService;
        _promotionalBalance = promotionalBalance;
        _targetResolver = targetResolver;
        _discountEngine = discountEngine;
        _teacherAccounting = teacherAccounting ?? new TeacherAccountingService(db);
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<bool>> Handle(PurchaseContentCommand request, CancellationToken ct)
    {
        try
        {
            await using var purchaseTransaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            // 1. Validate content exists and get its price
            decimal price = 0;
            string contentName = "";
            PublicExamProduct? publicExamProduct = null;

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
                case CodeType.Exam:
                    publicExamProduct = await _db.PublicExamProducts
                        .Include(x => x.Exam)
                        .FirstOrDefaultAsync(x => x.Id == request.ContentId || x.ExamId == request.ContentId, ct);
                    if (publicExamProduct == null) return ApiResponse<bool>.Fail("الامتحان العام غير موجود");
                    if (!publicExamProduct.IsPublished || publicExamProduct.DisabledAt != null)
                        return ApiResponse<bool>.Fail("الامتحان العام غير متاح للشراء حالياً.");
                    if (publicExamProduct.AvailableFrom.HasValue && publicExamProduct.AvailableFrom.Value > DateTime.UtcNow)
                        return ApiResponse<bool>.Fail("الامتحان العام لم يبدأ بعد.");
                    if (publicExamProduct.AvailableUntil.HasValue && publicExamProduct.AvailableUntil.Value <= DateTime.UtcNow)
                        return ApiResponse<bool>.Fail("انتهت صلاحية الامتحان العام.");
                    price = publicExamProduct.IsPaid ? publicExamProduct.Price : 0;
                    contentName = publicExamProduct.Exam.Title;
                    break;
                default:
                    return ApiResponse<bool>.Fail("نوع المحتوى غير مدعوم للشراء.");
            }

            var target = request.ContentType == CodeType.Exam && publicExamProduct != null
                ? await _targetResolver.ResolveAsync(SalesTargetType.PublicExam, publicExamProduct.Id, ct)
                : await _targetResolver.ResolveFromCodeTypeAsync(request.ContentType, request.ContentId, ct);
            if (target == null)
                return ApiResponse<bool>.Fail("تعذر تحديد هدف البيع.");

            if (target.TeacherId.HasValue)
            {
                var contentVisible = await _db.TeacherProfiles
                    .Where(t => t.Id == target.TeacherId.Value)
                    .Select(t => (bool?)t.IsContentVisibleToStudents)
                    .FirstOrDefaultAsync(ct);
                if (contentVisible == false)
                    return ApiResponse<bool>.Fail("المحتوى غير متاح للشراء حالياً.");
            }

            if (_academicScope != null)
            {
                var (ownerType, ownerId) = ResolveAcademicOwner(request.ContentType, request.ContentId, publicExamProduct);
                if (ownerType.HasValue)
                {
                    var academicResult = await _academicScope.ValidateStudentCanUseTargetAsync(
                        ownerType.Value,
                        ownerId,
                        request.StudentId,
                        ct);
                    if (!academicResult.IsEligible)
                    {
                        _db.AuditLogs.Add(new AuditLog
                        {
                            Action = "AcademicScopeDeniedPurchase",
                            EntityType = request.ContentType.ToString(),
                            EntityId = ownerId,
                            PerformedByUserId = request.StudentId,
                            NewValues = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                request.ContentType,
                                request.ContentId,
                                ownerType = ownerType.Value.ToString(),
                                ownerId,
                                academicResult.ErrorCode
                            }),
                            CreatedAt = DateTime.UtcNow
                        });
                        await _db.SaveChangesAsync(ct);
                        await purchaseTransaction.CommitAsync(ct);
                        return ApiResponse<bool>.Fail(
                            academicResult.Message ?? "هذا المحتوى غير متاح لنطاقك الدراسي الحالي.",
                            new List<string> { academicResult.ErrorCode ?? "ACADEMIC_SCOPE_DENIED" });
                    }
                }
            }

            var purchaseOperationId = Guid.NewGuid();
            var grossPrice = price;

            // Check if this is a repurchase of a lesson with exhausted/locked video views or rejected watch requests
            bool isRepurchase = false;
            if (request.ContentType == CodeType.Lesson)
            {
                var lessonVideos = await _db.LessonVideos
                    .Where(v => v.LessonId == request.ContentId)
                    .ToListAsync(ct);

                if (lessonVideos.Any())
                {
                    var videoIds = lessonVideos.Select(v => v.Id).ToList();
                    var watchEvents = await _db.VideoWatchEvents
                        .Where(we => we.UserId == request.StudentId && videoIds.Contains(we.LessonVideoId))
                        .ToListAsync(ct);

                    var hasRejectedRequest = await _db.ExtraWatchRequests
                        .AnyAsync(r => r.UserId == request.StudentId && videoIds.Contains(r.LessonVideoId) && r.Status == RequestStatus.Rejected, ct);

                    bool hasExhaustedVideo = lessonVideos.Any(v => {
                        var we = watchEvents.FirstOrDefault(e => e.LessonVideoId == v.Id);
                        if (we == null) return false;
                        int maxCount = we.CustomMaxWatchCount ?? v.MaxWatchCount;
                        return we.IsLocked || (maxCount > 0 && we.WatchCount >= maxCount);
                    });

                    if (hasExhaustedVideo || hasRejectedRequest)
                    {
                        isRepurchase = true;
                    }
                }
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
                case CodeType.Exam:
                    var productId = publicExamProduct?.Id ?? request.ContentId;
                    alreadyPurchased = await _db.StudentAccessGrants.AnyAsync(g =>
                        g.UserId == request.StudentId &&
                        g.GrantType == CodeType.Exam &&
                        g.PublicExamProductId == productId &&
                        g.IsActive, ct);
                    break;
            }

            if (alreadyPurchased && !isRepurchase)
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
                await purchaseTransaction.CommitAsync(ct);
                return ApiResponse<bool>.Fail("تم شراء هذا المحتوى مسبقاً");
            }

            // 3. Check if student already has access from a HIGHER-LEVEL grant
            // Hierarchy: Package > Term > Section (Month) > Lesson
            // If they own the parent, they can't buy the child (it's already included)
            string? coveredBy = null;
            if (!isRepurchase)
            {
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
            }

            if (coveredBy != null)
            {
                return ApiResponse<bool>.Fail($"أنت مشترك بالفعل في {coveredBy} — لا يمكن شراء {contentName} بشكل منفصل لأنها مغطاة بالاشتراك الحالي.");
            }

            var discount = await _discountEngine.CommitAsync(
                request.StudentId,
                target,
                new DiscountInput(request.CouponCodes ?? Array.Empty<string>(), request.PrintableCodes ?? Array.Empty<string>()),
                purchaseOperationId,
                ct);
            if (!discount.Success)
                return ApiResponse<bool>.Fail(discount.Error ?? "تعذر تطبيق الخصم.");
            price = Math.Max(0, price - discount.TotalDiscountAmount);

            var teacherId = await _promotionalBalance.ResolveTeacherIdAsync(request.ContentType, request.ContentId, ct);
            var funding = await _promotionalBalance.ConsumeAsync(
                request.StudentId,
                teacherId,
                request.ContentType,
                request.ContentId,
                price,
                ct);

            if (funding.PaidAmount > 0)
            {
                try
                {
                    await _balanceService.DeductBalance(
                        request.StudentId,
                        funding.PaidAmount,
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
                    await purchaseTransaction.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail($"الرصيد المتاح لا يكفي لشراء {contentName} بسعر ({price} ج.م)");
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
                case CodeType.Exam:
                    grant.PublicExamProductId = publicExamProduct?.Id ?? request.ContentId;
                    grant.ExamId = publicExamProduct?.ExamId ?? request.ContentId;
                    break;
            }

            _db.StudentAccessGrants.Add(grant);
            decimal teacherShareImpact = 0m;
            decimal platformShareImpact = funding.PaidAmount;

            if (target.TeacherId.HasValue)
            {
                var teacherProfile = await _db.TeacherProfiles
                    .FirstOrDefaultAsync(t => t.Id == target.TeacherId.Value, ct);
                var student = await _db.Users
                    .FirstOrDefaultAsync(u => u.Id == request.StudentId, ct);

                if (teacherProfile != null)
                {
                    var commissionRate = Math.Clamp(teacherProfile.CommissionRate, 0m, 100m);
                    teacherShareImpact = funding.PaidAmount > 0
                        ? Math.Round(funding.PaidAmount * commissionRate / 100m, 2, MidpointRounding.AwayFromZero)
                        : 0m;
                    platformShareImpact = Math.Max(0m, funding.PaidAmount - teacherShareImpact);

                    await _teacherAccounting.RecordEventAsync(new TeacherFinancialEventInput(
                        request.ContentType == CodeType.Exam
                            ? TeacherFinancialSourceType.PublicExamPurchase
                            : TeacherFinancialSourceType.DirectPurchase,
                        purchaseOperationId,
                        request.StudentId,
                        target.TargetType,
                        target.TargetId ?? request.ContentId,
                        grossPrice,
                        discount.TotalDiscountAmount,
                        funding.PaidAmount,
                        funding.PromotionalAmount,
                        platformShareImpact,
                        $"purchase:{purchaseOperationId}",
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            request.ContentType,
                            request.ContentId,
                            contentName,
                            discountedPrice = price,
                            discountLines = discount.Lines,
                            fundingOperationId = funding.OperationId
                        }),
                        DateTime.UtcNow,
                        TeacherFinancialReviewStatus.AutoApproved,
                        new[]
                        {
                            new TeacherFinancialAllocationInput(
                                teacherProfile.Id,
                                TeacherAllocationMode.CommissionRate,
                                commissionRate,
                                funding.PaidAmount,
                                teacherShareImpact,
                                platformShareImpact,
                                student?.FullName,
                                student?.PhoneNumber,
                                contentName)
                        }), ct);
                }
            }

            _db.SalesFinancialEffects.Add(new SalesFinancialEffect
            {
                PurchaseOperationId = purchaseOperationId,
                StudentId = request.StudentId,
                TargetType = target.TargetType,
                TargetId = target.TargetId ?? request.ContentId,
                GrossAmount = grossPrice,
                CouponDiscountAmount = discount.CouponDiscountAmount,
                PrintableCodeDiscountAmount = discount.PrintableCodeDiscountAmount,
                PromotionalAmount = funding.PromotionalAmount,
                PaidAmount = funding.PaidAmount,
                TeacherId = target.TeacherId,
                TeacherShareImpact = teacherShareImpact,
                PlatformShareImpact = platformShareImpact,
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    request.ContentType,
                    request.ContentId,
                    discountedPrice = price,
                    discountLines = discount.Lines
                })
            });

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
                    price,
                    grossAmount = grossPrice,
                    couponDiscountAmount = discount.CouponDiscountAmount,
                    printableCodeDiscountAmount = discount.PrintableCodeDiscountAmount,
                    promotionalAmount = funding.PromotionalAmount,
                    paidAmount = funding.PaidAmount,
                    fundingOperationId = funding.OperationId
                })
            };
            _db.OutboxEvents.Add(purchaseCompletedEvent);

            if (isRepurchase)
            {
                var lessonVideos = await _db.LessonVideos
                    .Where(v => v.LessonId == request.ContentId)
                    .ToListAsync(ct);
                var videoIds = lessonVideos.Select(v => v.Id).ToList();

                var watchEvents = await _db.VideoWatchEvents
                    .Where(we => we.UserId == request.StudentId && videoIds.Contains(we.LessonVideoId))
                    .ToListAsync(ct);

                foreach (var we in watchEvents)
                {
                    we.WatchCount = 0;
                    we.IsLocked = false;
                    we.CustomMaxWatchCount = null;
                    we.TimeWatchedInSeconds = 0;
                    we.UpdatedAt = DateTime.UtcNow;
                }

                var requestsToDelete = await _db.ExtraWatchRequests
                    .Where(r => r.UserId == request.StudentId && videoIds.Contains(r.LessonVideoId))
                    .ToListAsync(ct);
                _db.ExtraWatchRequests.RemoveRange(requestsToDelete);

                foreach (var videoId in videoIds)
                {
                    var v = lessonVideos.FirstOrDefault(e => e.Id == videoId);
                    var outboxEvent = new OutboxEvent
                    {
                        Type = "ExtraWatchRequestUpdated",
                        TargetUserId = request.StudentId.ToString(),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            lessonId = request.ContentId,
                            videoId = videoId,
                            status = "Approved",
                            allowedWatchCount = v?.MaxWatchCount ?? 0
                        })
                    };
                    _db.OutboxEvents.Add(outboxEvent);
                }
            }

            await _db.SaveChangesAsync(ct);
            await purchaseTransaction.CommitAsync(ct);

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

    private static (StudentFacingScopeOwnerType? OwnerType, Guid OwnerId) ResolveAcademicOwner(
        CodeType contentType,
        Guid contentId,
        PublicExamProduct? publicExamProduct)
    {
        return contentType switch
        {
            CodeType.Package => (StudentFacingScopeOwnerType.Package, contentId),
            CodeType.Term => (StudentFacingScopeOwnerType.Term, contentId),
            CodeType.Month => (StudentFacingScopeOwnerType.ContentSection, contentId),
            CodeType.Lesson => (StudentFacingScopeOwnerType.Lesson, contentId),
            CodeType.Exam => (StudentFacingScopeOwnerType.PublicExamProduct, publicExamProduct?.Id ?? contentId),
            _ => (null, contentId)
        };
    }
}
