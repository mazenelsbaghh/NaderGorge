using System.Data;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Codes.Commands;

// ── Activate / Redeem Code Command ──────────────────────────────────────────
public record ActivateCodeCommand(Guid UserId, string Code) : IRequest<ApiResponse<ActivateCodeResponse>>;

public record ActivateCodeResponse(
    Guid GrantId,
    string Message,
    CodeType GrantType,
    string? RedirectUrl
);

public class ActivateCodeCommandValidator : AbstractValidator<ActivateCodeCommand>
{
    public ActivateCodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MinimumLength(6);
    }
}

public class ActivateCodeCommandHandler : IRequestHandler<ActivateCodeCommand, ApiResponse<ActivateCodeResponse>>
{
    private readonly IAppDbContext _db;
    private readonly IJobEnqueuer _jobEnqueuer;
    private readonly TeacherAccountingService _teacherAccounting;
    private readonly TeacherAgreementResolver _agreementResolver;
    private readonly IAcademicScopeService? _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public ActivateCodeCommandHandler(
        IAppDbContext db,
        IJobEnqueuer jobEnqueuer,
        TeacherAccountingService? teacherAccounting = null,
        TeacherAgreementResolver? agreementResolver = null,
        IAcademicScopeService? academicScope = null,
        IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _jobEnqueuer = jobEnqueuer;
        _teacherAccounting = teacherAccounting ?? new TeacherAccountingService(db);
        _agreementResolver = agreementResolver ?? new TeacherAgreementResolver(db);
        _academicScope = academicScope;
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(db);
    }

    public async Task<ApiResponse<ActivateCodeResponse>> Handle(ActivateCodeCommand request, CancellationToken ct)
    {
        try
        {
            await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
                ?? throw new KeyNotFoundException("User not found");

            var accessCode = await _db.AccessCodes
                .AsNoTracking()
                .Include(c => c.CodeGroup)
                    .ThenInclude(g => g.CodeVideoTargets)
                .FirstOrDefaultAsync(c => c.CodePlaintext == request.Code, ct)
                ?? throw new KeyNotFoundException("Invalid or already used code");

            if (accessCode.IsConsumed)
                throw new KeyNotFoundException("Invalid or already used code");

            // Check expiration
            var now = DateTime.UtcNow;
            if (accessCode.ExpiresAt.HasValue && accessCode.ExpiresAt.Value < now)
                throw new InvalidOperationException("This code has expired.");

            if (accessCode.CodeGroup.ExpiresAt.HasValue && accessCode.CodeGroup.ExpiresAt.Value < now)
                throw new InvalidOperationException("This code group has expired.");

            var codeGroup = accessCode.CodeGroup;
            var archiveTarget = codeGroup.CodeType switch
            {
                CodeType.Package when codeGroup.PackageId.HasValue => (ContentArchiveTargetType.Package, codeGroup.PackageId.Value),
                CodeType.Term when codeGroup.TermId.HasValue => (ContentArchiveTargetType.Term, codeGroup.TermId.Value),
                CodeType.Month when codeGroup.ContentSectionId.HasValue => (ContentArchiveTargetType.Section, codeGroup.ContentSectionId.Value),
                CodeType.Lesson when codeGroup.LessonId.HasValue => (ContentArchiveTargetType.Lesson, codeGroup.LessonId.Value),
                CodeType.Exam when codeGroup.ExamId.HasValue => (ContentArchiveTargetType.Exam, codeGroup.ExamId.Value),
                _ => ((ContentArchiveTargetType TargetType, Guid TargetId)?)null
            };
            if (archiveTarget.HasValue && !await _archiveAccess.CanAcquireAsync(archiveTarget.Value.TargetType, archiveTarget.Value.TargetId, ct))
                return ApiResponse<ActivateCodeResponse>.Fail("هذا المحتوى مؤرشف ولا يقبل اشتراكات جديدة.", ["CONTENT_ARCHIVED"]);

            var academicResult = await ValidateCodeAcademicScopeAsync(codeGroup, user.Id, ct);
            if (!academicResult.IsEligible)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    Action = "AcademicScopeDeniedCodeActivation",
                    EntityType = "AccessCode",
                    EntityId = accessCode.Id,
                    PerformedByUserId = user.Id,
                    NewValues = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        codeGroupId = codeGroup.Id,
                        codeType = codeGroup.CodeType.ToString(),
                        academicResult.ErrorCode
                    }),
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ApiResponse<ActivateCodeResponse>.Fail(
                    academicResult.Message ?? "هذا الكود غير متاح لنطاقك الدراسي الحالي.",
                    new List<string> { academicResult.ErrorCode ?? "ACADEMIC_SCOPE_DENIED" });
            }

            var existingAccessMessage = await GetExistingAccessMessageAsync(codeGroup, user.Id, now, ct);
            if (existingAccessMessage != null)
                return ApiResponse<ActivateCodeResponse>.Fail(existingAccessMessage);

            int consumedRows;
            if (_db is DbContext efDb && efDb.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                var entry = await _db.AccessCodes.FirstOrDefaultAsync(c => c.Id == accessCode.Id && !c.IsConsumed, ct);
                if (entry != null)
                {
                    entry.IsConsumed = true;
                    entry.ConsumedByUserId = user.Id;
                    entry.ConsumedAt = now;
                    entry.UpdatedAt = now;
                    _db.AccessCodes.Update(entry);
                    await _db.SaveChangesAsync(ct);
                    consumedRows = 1;
                }
                else
                {
                    consumedRows = 0;
                }
            }
            else
            {
                consumedRows = await _db.AccessCodes
                    .Where(c => c.Id == accessCode.Id && !c.IsConsumed)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(c => c.IsConsumed, true)
                        .SetProperty(c => c.ConsumedByUserId, user.Id)
                        .SetProperty(c => c.ConsumedAt, now)
                        .SetProperty(c => c.UpdatedAt, now), ct);
            }

            if (consumedRows != 1)
                throw new KeyNotFoundException("Invalid or already used code");

            var codeType = codeGroup.CodeType;
            Guid grantId;
            string? redirectUrl = null;

            if (codeType == CodeType.Balance)
            {
                var amount = codeGroup.BalanceAmount ?? 0m;
                if (amount <= 0)
                    throw new InvalidOperationException("Invalid balance code amount.");

                if (!codeGroup.TeacherId.HasValue)
                {
                    var balance = await _db.StudentBalances.FirstOrDefaultAsync(b => b.UserId == user.Id, ct);
                    if (balance == null)
                    {
                        balance = new StudentBalance
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            CurrentBalance = 0m
                        };
                        _db.StudentBalances.Add(balance);
                    }

                    balance.CurrentBalance += amount;
                    balance.UpdatedAt = DateTime.UtcNow;

                    var transactionEntry = new BalanceTransaction
                    {
                        Id = Guid.NewGuid(),
                        StudentBalanceId = balance.Id,
                        Amount = amount,
                        BalanceAfter = balance.CurrentBalance,
                        TransactionType = "CodeRedemption",
                        ReferenceId = accessCode.Id,
                        Description = $"شحن رصيد عام من كود {codeGroup.Name}"
                    };
                    _db.BalanceTransactions.Add(transactionEntry);
                    grantId = transactionEntry.Id;

                    var generalMaskedCode = request.Code.Length > 4 ? request.Code[..4] + "****" : "****";
                    _db.AuditLogs.Add(new AuditLog
                    {
                        Action = "ActivateCode",
                        EntityType = "AccessCode",
                        EntityId = accessCode.Id,
                        PerformedByUserId = user.Id,
                        NewValues = $"CodePlaintext: {generalMaskedCode}, Type: {CodeType.Balance}, Amount: {amount}, Scope: PlatformBalance",
                        CreatedAt = DateTime.UtcNow
                    });
                    _db.OutboxEvents.Add(new OutboxEvent
                    {
                        Type = "BalanceChanged",
                        TargetUserId = user.Id.ToString(),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            currentBalance = balance.CurrentBalance,
                            amount,
                            formattedBalance = $"{balance.CurrentBalance:F2} جنيها"
                        })
                    });

                    await _db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return ApiResponse<ActivateCodeResponse>.Ok(
                        new ActivateCodeResponse(grantId, $"تم شحن رصيدك العام بـ {amount} جنيه", CodeType.Balance, "/student/balance"));
                }

                var teacherName = await _db.TeacherProfiles
                    .Where(x => x.Id == codeGroup.TeacherId.Value)
                    .Select(x => x.User.FullName)
                    .FirstOrDefaultAsync(ct);
                var giftRecipient = new GiftRecipient
                {
                    StudentId = user.Id,
                    Status = GiftRecipientStatus.Active,
                    OutcomeCode = "GRANTED",
                    OutcomeMessage = $"تم شحن رصيد مخصص للمدرس {teacherName ?? "المحدد"} من كود {codeGroup.Name}"
                };
                var scopedBalance = new PromotionalBalanceAllocation
                {
                    StudentId = user.Id,
                    TeacherId = codeGroup.TeacherId.Value,
                    OriginalAmount = amount,
                    AvailableAmount = amount,
                    ExpiresAt = codeGroup.ExpireActivatedAccess ? codeGroup.ExpiresAt : null,
                    GiftRecipient = giftRecipient
                };
                var issuance = new GiftIssuance
                {
                    RequestId = Guid.NewGuid(),
                    TargetType = GiftTargetType.TeacherBalance,
                    TeacherId = codeGroup.TeacherId.Value,
                    Amount = amount,
                    ExpiresAt = codeGroup.ExpireActivatedAccess ? codeGroup.ExpiresAt : null,
                    Reason = $"Teacher scoped balance code: {codeGroup.Name}",
                    IssuedByUserId = codeGroup.CreatedByUserId,
                    Status = GiftIssuanceStatus.Active,
                    Recipients = { giftRecipient }
                };
                giftRecipient.PromotionalBalanceAllocation = scopedBalance;
                _db.GiftIssuances.Add(issuance);
                grantId = scopedBalance.Id;

                var balanceMaskedCode = request.Code.Length > 4 ? request.Code[..4] + "****" : "****";
                _db.AuditLogs.Add(new AuditLog
                {
                    Action = "ActivateCode",
                    EntityType = "AccessCode",
                    EntityId = accessCode.Id,
                    PerformedByUserId = user.Id,
                    NewValues = $"CodePlaintext: {balanceMaskedCode}, Type: {CodeType.Balance}, Amount: {amount}, TeacherId: {codeGroup.TeacherId.Value}, Scope: TeacherBalance",
                    CreatedAt = DateTime.UtcNow
                });
                _db.OutboxEvents.Add(new OutboxEvent
                {
                    Type = "BalanceChanged",
                    TargetUserId = user.Id.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        scopedTeacherId = codeGroup.TeacherId,
                        scopedTeacherName = teacherName,
                        promotionalAmount = amount,
                        formattedBalance = $"{amount:F2} جنيها"
                    })
                });

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ApiResponse<ActivateCodeResponse>.Ok(
                    new ActivateCodeResponse(grantId, $"تم شحن رصيدك للمدرس {teacherName ?? "المحدد"} بـ {amount} جنيه", CodeType.Balance, "/student/balance"));
            }

            // For all other code types: create access grants
            if (codeType == CodeType.Video)
            {
                // Video codes may target explicit videos or all videos of a type within an optional scope.
                var videoTargets = codeGroup.CodeVideoTargets;
                if (!videoTargets.Any() && !codeGroup.VideoTypeId.HasValue)
                    throw new InvalidOperationException("Video code has no target videos or video type.");

                Guid? lastGrantId = null;
                if (codeGroup.VideoTypeId.HasValue && codeGroup.IncludeFutureVideos)
                {
                    var grant = new StudentAccessGrant
                    {
                        UserId = user.Id,
                        GrantType = CodeType.Video,
                        VideoTypeId = codeGroup.VideoTypeId,
                        PackageId = codeGroup.PackageId,
                        TermId = codeGroup.TermId,
                        ContentSectionId = codeGroup.ContentSectionId,
                        LessonId = codeGroup.LessonId,
                        AccessCodeId = accessCode.Id,
                        IsActive = true,
                        ExpiresAt = codeGroup.ExpireActivatedAccess ? codeGroup.ExpiresAt : null
                    };
                    _db.StudentAccessGrants.Add(grant);
                    lastGrantId = grant.Id;
                }

                foreach (var target in videoTargets)
                {
                    var grant = new StudentAccessGrant
                    {
                        UserId = user.Id,
                        LessonVideoId = target.LessonVideoId,
                        GrantType = CodeType.Video,
                        AccessCodeId = accessCode.Id,
                        IsActive = true,
                        ExpiresAt = codeGroup.ExpireActivatedAccess ? codeGroup.ExpiresAt : null
                    };
                    _db.StudentAccessGrants.Add(grant);
                    lastGrantId = grant.Id;
                }
                grantId = lastGrantId!.Value;
                redirectUrl = "/student/content";
            }
            else
            {
                // Package, Term, Month, Lesson, Exam: create single access grant
                var grant = new StudentAccessGrant
                {
                    UserId = user.Id,
                    GrantType = codeType,
                    AccessCodeId = accessCode.Id,
                    IsActive = true,
                    ExpiresAt = codeGroup.ExpireActivatedAccess ? codeGroup.ExpiresAt : null
                };

                // Set the appropriate target FK
                switch (codeType)
                {
                    case CodeType.Package:
                        grant.PackageId = codeGroup.PackageId;
                        redirectUrl = codeGroup.PackageId.HasValue ? $"/student/packages/{codeGroup.PackageId}" : "/student/packages";
                        break;
                    case CodeType.Term:
                        grant.TermId = codeGroup.TermId;
                        redirectUrl = $"/student/content?termId={codeGroup.TermId}";
                        break;
                    case CodeType.Month:
                        grant.ContentSectionId = codeGroup.ContentSectionId;
                        redirectUrl = $"/student/content?sectionId={codeGroup.ContentSectionId}";
                        break;
                    case CodeType.Lesson:
                        grant.LessonId = codeGroup.LessonId;
                        redirectUrl = $"/student/lessons/{codeGroup.LessonId}";
                        break;
                    case CodeType.Exam:
                        grant.ExamId = codeGroup.ExamId;
                        grant.PublicExamProductId = codeGroup.PublicExamProductId;
                        redirectUrl = codeGroup.PublicExamProductId.HasValue
                            ? "/student/public-exams"
                            : $"/student/exams/{codeGroup.ExamId}";
                        break;
                }

                _db.StudentAccessGrants.Add(grant);
                grantId = grant.Id;
            }

            // Calculate and credit teacher commission (except for balance codes which don't directly purchase items)
            var financialTerms = await _db.CodeGroupFinancialTerms.AsNoTracking()
                .FirstOrDefaultAsync(x => x.CodeGroupId == codeGroup.Id, ct);
            var accountingTrigger = financialTerms?.Trigger
                ?? (codeGroup.AccountingTiming == CodeAccountingTiming.Immediate
                    ? TeacherAgreementTrigger.CodeDelivery
                    : TeacherAgreementTrigger.CodeActivation);

            // Delivery-billed batches were recorded by the audited confirmation endpoint.
            // Redeeming one of their codes must only grant access, never create a second due.
            if (codeType != CodeType.Balance && accountingTrigger == TeacherAgreementTrigger.CodeActivation)
            {
                decimal itemPrice = 0;
                switch (codeType)
                {
                    case CodeType.Package:
                        if (codeGroup.PackageId.HasValue)
                        {
                            var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == codeGroup.PackageId.Value, ct);
                            if (pkg != null) itemPrice = pkg.Price;
                        }
                        break;
                    case CodeType.Term:
                        if (codeGroup.TermId.HasValue)
                        {
                            var term = await _db.Terms.FirstOrDefaultAsync(t => t.Id == codeGroup.TermId.Value, ct);
                            if (term != null) itemPrice = term.Price;
                        }
                        break;
                    case CodeType.Month:
                        if (codeGroup.ContentSectionId.HasValue)
                        {
                            var section = await _db.ContentSections.FirstOrDefaultAsync(s => s.Id == codeGroup.ContentSectionId.Value, ct);
                            if (section != null) itemPrice = section.Price;
                        }
                        break;
                    case CodeType.Lesson:
                        if (codeGroup.LessonId.HasValue)
                        {
                            var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == codeGroup.LessonId.Value, ct);
                            if (lesson != null) itemPrice = lesson.Price;
                        }
                        break;
                }

                itemPrice = Math.Max(0m, itemPrice);
                var discountPercentage = Math.Clamp(codeGroup.DiscountPercentage ?? 0m, 0m, 100m);
                var finalPrice = itemPrice * (1m - discountPercentage / 100m);

                var teacherProfile = codeGroup.TeacherId.HasValue
                    ? await _db.TeacherProfiles.FirstOrDefaultAsync(tp => tp.Id == codeGroup.TeacherId.Value, ct)
                    : null;

                var targetType = codeType switch
                {
                    CodeType.Package => SalesTargetType.Package,
                    CodeType.Term => SalesTargetType.Term,
                    CodeType.Month => SalesTargetType.ContentSection,
                    CodeType.Lesson => SalesTargetType.Lesson,
                    CodeType.Exam => SalesTargetType.PublicExam,
                    _ => SalesTargetType.Platform
                };
                var targetId = codeGroup.PackageId
                    ?? codeGroup.TermId
                    ?? codeGroup.ContentSectionId
                    ?? codeGroup.LessonId
                    ?? codeGroup.PublicExamProductId
                    ?? codeGroup.ExamId
                    ?? codeGroup.Id;
                var occurredAt = DateTime.UtcNow;
                TeacherAgreementResolution? agreement = null;
                if (teacherProfile != null)
                {
                    if (financialTerms?.AgreementId is Guid agreementId)
                    {
                        var selected = await _db.TeacherFinancialAgreements.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == agreementId && x.TeacherId == teacherProfile.Id && x.IsActive
                                && x.Trigger == TeacherAgreementTrigger.CodeActivation
                                && x.EffectiveFrom <= occurredAt && (x.EffectiveTo == null || x.EffectiveTo >= occurredAt), ct);
                        if (selected != null)
                        {
                            agreement = new TeacherAgreementResolution(selected.Id, selected.ScopeType, selected.ScopeId,
                                selected.AllocationMode, selected.AllocationValue, selected.PriceBasis);
                        }
                    }
                    agreement ??= await _agreementResolver.ResolveAsync(teacherProfile.Id, TeacherAgreementTrigger.CodeActivation,
                        await _agreementResolver.BuildScopesAsync(targetType, targetId, ct), occurredAt, ct);
                }
                var (allocationMode, teacherShare, basisAmount) = agreement is null
                    ? (TeacherAllocationMode.CommissionRate, 0m, finalPrice)
                    : TeacherAgreementResolver.CalculateAllocation(agreement, itemPrice, finalPrice);
                var platformShare = finalPrice - teacherShare;

                if (teacherProfile != null)
                {
                    var activationLog = new AccessCodeActivationLog
                    {
                        Id = Guid.NewGuid(),
                        AccessCodeId = accessCode.Id,
                        StudentId = user.Id,
                        PackageId = codeGroup.PackageId,
                        TeacherId = teacherProfile.Id,
                        Price = finalPrice,
                        CommissionRate = allocationMode is TeacherAllocationMode.Percentage or TeacherAllocationMode.CommissionRate
                            ? agreement!.AllocationValue
                            : 0m,
                        CommissionEarned = teacherShare,
                        ActivatedAt = DateTime.UtcNow
                    };
                    _db.AccessCodeActivationLogs.Add(activationLog);
                }

                await _teacherAccounting.RecordEventAsync(new TeacherFinancialEventInput(
                    TeacherFinancialSourceType.AccessCodeActivation,
                    accessCode.Id,
                    user.Id,
                    targetType,
                    targetId,
                    itemPrice,
                    itemPrice - finalPrice,
                    finalPrice,
                    0m,
                    platformShare,
                    $"access-code:{accessCode.Id}",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        codeGroupId = codeGroup.Id,
                        codeGroup.Name,
                        codeType = codeType.ToString(),
                        accessCode.SerialNumber
                    }),
                    occurredAt,
                    TeacherFinancialReviewStatus.AutoApproved,
                    teacherProfile != null && teacherShare > 0m
                        ? new[]
                        {
                            new TeacherFinancialAllocationInput(
                                teacherProfile.Id,
                                allocationMode,
                                agreement!.AllocationValue,
                                basisAmount,
                                teacherShare,
                                platformShare,
                                user.FullName,
                                user.PhoneNumber,
                                codeGroup.Name,
                                accessCode.SerialNumber,
                                AgreementId: agreement!.AgreementId,
                                AgreementScopeType: agreement.ScopeType,
                                AgreementScopeId: agreement.ScopeId,
                                AgreementAllocationMode: agreement.AllocationMode,
                                PriceBasis: agreement.PriceBasis)
                        }
                        : Array.Empty<TeacherFinancialAllocationInput>()), ct);
            }

            var maskedCode = request.Code.Length > 4 ? request.Code[..4] + "****" : "****";
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "ActivateCode",
                EntityType = "AccessCode",
                EntityId = accessCode.Id,
                PerformedByUserId = user.Id,
                NewValues = $"CodePlaintext: {maskedCode}, Type: {codeType}, GrantId: {grantId}",
                CreatedAt = DateTime.UtcNow
            });

            var message = codeType switch
            {
                CodeType.Package => codeGroup.PackageId.HasValue ? "تم تفعيل الباكدج بنجاح!" : "تم إضافة باكدج عام إلى حسابك!",
                CodeType.Term => "تم تفعيل الترم بنجاح!",
                CodeType.Month => "تم تفعيل الشهر بنجاح!",
                CodeType.Lesson => "تم تفعيل الحصة بنجاح!",
                CodeType.Video => "تم تفعيل الفيديوهات بنجاح!",
                CodeType.Exam => codeGroup.PublicExamProductId.HasValue ? "تم تفعيل الامتحان العام بنجاح!" : "تم تفعيل الامتحان بنجاح!",
                _ => "تم تفعيل الكود بنجاح!"
            };

            var outboxEvent = new OutboxEvent
            {
                Type = "CodeActivated",
                TargetUserId = user.Id.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    codeType = codeType.ToString(),
                    referenceId = grantId.ToString(),
                    message = message
                })
            };
            _db.OutboxEvents.Add(outboxEvent);

            if (codeType == CodeType.Package && codeGroup.PackageId.HasValue)
            {
                var packageAccessGrantedEvent = new OutboxEvent
                {
                    Type = "PackageAccessGranted",
                    TargetUserId = user.Id.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        userId = user.Id,
                        packageId = codeGroup.PackageId.Value
                    })
                };
                _db.OutboxEvents.Add(packageAccessGrantedEvent);
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _jobEnqueuer.EnqueueJobAsync("notifications", "parent-push", new
            {
                StudentId = user.Id,
                Title = "شراء جديد للطالب",
                Body = $"تم تفعيل {GetArabicGrantName(codeType)} للطالب {user.FullName}.",
                Category = "Purchase",
                ParentPush = true
            });

            return ApiResponse<ActivateCodeResponse>.Ok(
                new ActivateCodeResponse(grantId, message, codeType, redirectUrl));
        }
        catch (Exception ex) when (IsConcurrencyFailure(ex))
        {
            return ApiResponse<ActivateCodeResponse>.Fail("Invalid or already used code");
        }
    }

    private static bool IsConcurrencyFailure(Exception ex)
    {
        return ex.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("concurrent update", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException != null && IsConcurrencyFailure(ex.InnerException));
    }

    private static string GetArabicGrantName(CodeType codeType) => codeType switch
    {
        CodeType.Package => "باقة جديدة",
        CodeType.Term => "ترم جديد",
        CodeType.Month => "شهر جديد",
        CodeType.Lesson => "حصة جديدة",
        CodeType.Video => "فيديو جديد",
        CodeType.Exam => "امتحان جديد",
        _ => "محتوى جديد"
    };

    /// <summary>
    /// Prevents a student from spending a code on content they can already access.
    /// A package grant represents the full academic year, so it also covers its terms,
    /// months, and lessons.
    /// </summary>
    private async Task<string?> GetExistingAccessMessageAsync(
        CodeGroup codeGroup,
        Guid userId,
        DateTime now,
        CancellationToken ct)
    {
        var activeGrants = _db.StudentAccessGrants
            .AsNoTracking()
            .Where(grant => grant.UserId == userId
                && grant.IsActive
                && (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now));

        switch (codeGroup.CodeType)
        {
            case CodeType.Package when codeGroup.PackageId.HasValue:
            {
                var package = await _db.Packages.AsNoTracking()
                    .Where(item => item.Id == codeGroup.PackageId.Value)
                    .Select(item => new { item.Id, item.Name })
                    .FirstOrDefaultAsync(ct);

                if (package != null && await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Package && grant.PackageId == package.Id, ct))
                {
                    return $"لديك الباقة «{package.Name}» بالفعل على حسابك. الكود لم يُستخدم.";
                }

                break;
            }
            case CodeType.Term when codeGroup.TermId.HasValue:
            {
                var term = await _db.Terms.AsNoTracking()
                    .Where(item => item.Id == codeGroup.TermId.Value)
                    .Select(item => new { item.Id, item.Title, item.PackageId, PackageName = item.Package.Name })
                    .FirstOrDefaultAsync(ct);

                if (term == null)
                    break;

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Term && grant.TermId == term.Id, ct))
                {
                    return $"أنت مشترك بالفعل في الترم «{term.Title}». الكود لم يُستخدم.";
                }

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Package && grant.PackageId == term.PackageId, ct))
                {
                    return $"لديك الترم «{term.Title}» بالفعل ضمن الباقة «{term.PackageName}». الكود لم يُستخدم.";
                }

                break;
            }
            case CodeType.Month when codeGroup.ContentSectionId.HasValue:
            {
                var section = await _db.ContentSections.AsNoTracking()
                    .Where(item => item.Id == codeGroup.ContentSectionId.Value)
                    .Select(item => new
                    {
                        item.Id,
                        item.Title,
                        item.TermId,
                        item.Term.PackageId,
                        PackageName = item.Term.Package.Name
                    })
                    .FirstOrDefaultAsync(ct);

                if (section == null)
                    break;

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Month && grant.ContentSectionId == section.Id, ct))
                {
                    return $"لديك الشهر «{section.Title}» بالفعل على حسابك. الكود لم يُستخدم.";
                }

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Term && grant.TermId == section.TermId, ct))
                {
                    return $"لديك الشهر «{section.Title}» بالفعل ضمن الترم الخاص به. الكود لم يُستخدم.";
                }

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Package && grant.PackageId == section.PackageId, ct))
                {
                    return $"لديك الشهر «{section.Title}» بالفعل ضمن الباقة «{section.PackageName}». الكود لم يُستخدم.";
                }

                break;
            }
            case CodeType.Lesson when codeGroup.LessonId.HasValue:
            {
                var lesson = await _db.Lessons.AsNoTracking()
                    .Where(item => item.Id == codeGroup.LessonId.Value)
                    .Select(item => new
                    {
                        item.Id,
                        item.Title,
                        item.ContentSectionId,
                        item.ContentSection.TermId,
                        item.ContentSection.Term.PackageId,
                        PackageName = item.ContentSection.Term.Package.Name
                    })
                    .FirstOrDefaultAsync(ct);

                if (lesson == null)
                    break;

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Lesson && grant.LessonId == lesson.Id, ct))
                {
                    return $"لديك الحصة «{lesson.Title}» بالفعل على حسابك. الكود لم يُستخدم.";
                }

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Month && grant.ContentSectionId == lesson.ContentSectionId, ct))
                {
                    return $"لديك الحصة «{lesson.Title}» بالفعل ضمن الشهر الخاص بها. الكود لم يُستخدم.";
                }

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Term && grant.TermId == lesson.TermId, ct))
                {
                    return $"لديك الحصة «{lesson.Title}» بالفعل ضمن الترم الخاص بها. الكود لم يُستخدم.";
                }

                if (await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Package && grant.PackageId == lesson.PackageId, ct))
                {
                    return $"لديك الحصة «{lesson.Title}» بالفعل ضمن الباقة «{lesson.PackageName}». الكود لم يُستخدم.";
                }

                break;
            }
            case CodeType.Exam:
            {
                var hasExamAccess = codeGroup.PublicExamProductId.HasValue
                    ? await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Exam
                            && grant.PublicExamProductId == codeGroup.PublicExamProductId.Value, ct)
                    : codeGroup.ExamId.HasValue && await activeGrants.AnyAsync(
                        grant => grant.GrantType == CodeType.Exam && grant.ExamId == codeGroup.ExamId.Value, ct);

                if (hasExamAccess)
                    return "لديك هذا الامتحان بالفعل على حسابك. الكود لم يُستخدم.";

                break;
            }
        }

        return null;
    }

    private async Task<AcademicScopeCheckResult> ValidateCodeAcademicScopeAsync(CodeGroup codeGroup, Guid userId, CancellationToken ct)
    {
        if (_academicScope == null || codeGroup.CodeType == CodeType.Balance)
            return AcademicScopeCheckResult.Eligible();

        var targets = await ResolveAcademicTargetsAsync(codeGroup, ct);
        if (targets.Count == 0)
            targets.Add((StudentFacingScopeOwnerType.CodeGroup, codeGroup.Id));

        foreach (var (ownerType, ownerId) in targets)
        {
            var result = await _academicScope.ValidateStudentCanUseTargetAsync(ownerType, ownerId, userId, ct);
            if (!result.IsEligible)
                return result;
        }

        return AcademicScopeCheckResult.Eligible();
    }

    private async Task<List<(StudentFacingScopeOwnerType OwnerType, Guid OwnerId)>> ResolveAcademicTargetsAsync(CodeGroup codeGroup, CancellationToken ct)
    {
        var targets = new List<(StudentFacingScopeOwnerType OwnerType, Guid OwnerId)>();
        switch (codeGroup.CodeType)
        {
            case CodeType.Package when codeGroup.PackageId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Package, codeGroup.PackageId.Value));
                break;
            case CodeType.Term when codeGroup.TermId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Term, codeGroup.TermId.Value));
                break;
            case CodeType.Month when codeGroup.ContentSectionId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.ContentSection, codeGroup.ContentSectionId.Value));
                break;
            case CodeType.Lesson when codeGroup.LessonId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Lesson, codeGroup.LessonId.Value));
                break;
            case CodeType.Exam when codeGroup.PublicExamProductId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.PublicExamProduct, codeGroup.PublicExamProductId.Value));
                break;
            case CodeType.Exam when codeGroup.ExamId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Exam, codeGroup.ExamId.Value));
                break;
            case CodeType.Video:
                if (codeGroup.LessonId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.Lesson, codeGroup.LessonId.Value));
                else if (codeGroup.ContentSectionId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.ContentSection, codeGroup.ContentSectionId.Value));
                else if (codeGroup.TermId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.Term, codeGroup.TermId.Value));
                else if (codeGroup.PackageId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.Package, codeGroup.PackageId.Value));

                var videoTargets = await _db.CodeVideoTargets
                    .AsNoTracking()
                    .Where(x => x.CodeGroupId == codeGroup.Id)
                    .Select(x => x.LessonVideoId)
                    .ToListAsync(ct);
                targets.AddRange(videoTargets.Select(videoId => (StudentFacingScopeOwnerType.LessonVideo, videoId)));
                break;
        }

        return targets;
    }
}
