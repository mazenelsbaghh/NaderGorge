using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Sales;

public sealed record SaveSalesRuleCommand(SalesRuleRequest Request, Guid ActorId) : IRequest<ApiResponse<SalesRuleDto>>;
public sealed record CreateSalesCouponCommand(SalesCouponRequest Request, Guid ActorId) : IRequest<ApiResponse<SalesCouponDto>>;
public sealed record UpdateSalesCouponCommand(Guid Id, SalesCouponRequest Request, Guid ActorId) : IRequest<ApiResponse<SalesCouponDto>>;
public sealed record DisableSalesCouponCommand(Guid Id, string? Reason) : IRequest<ApiResponse<bool>>;
public sealed record SaveStackingPolicyCommand(StackingPolicyRequest Request, Guid ActorId) : IRequest<ApiResponse<StackingPolicyDto>>;
public sealed record SavePrintableTemplateCommand(PrintableTemplateRequest Request, Guid ActorId) : IRequest<ApiResponse<PrintableTemplateDto>>;
public sealed record CreatePrintableBatchCommand(PrintableBatchRequest Request, Guid ActorId) : IRequest<ApiResponse<PrintableBatchDto>>;
public sealed record SavePublicExamProductCommand(PublicExamProductRequest Request, Guid ActorId) : IRequest<ApiResponse<PublicExamProductDto>>;
public sealed record CreatePublicExamProductCommand(CreatePublicExamRequest Request, Guid ActorId) : IRequest<ApiResponse<PublicExamProductDto>>;
public sealed record DisablePublicExamProductCommand(Guid Id, Guid ActorId, string? Reason) : IRequest<ApiResponse<bool>>;

public sealed class SaveSalesRuleCommandHandler : IRequestHandler<SaveSalesRuleCommand, ApiResponse<SalesRuleDto>>
{
    private readonly IAppDbContext _db;
    private readonly ISalesTargetResolver _targetResolver;
    private readonly IAcademicScopeService? _academicScope;

    public SaveSalesRuleCommandHandler(IAppDbContext db, ISalesTargetResolver targetResolver, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _targetResolver = targetResolver;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<SalesRuleDto>> Handle(SaveSalesRuleCommand request, CancellationToken ct)
    {
        var r = request.Request;
        if (r.TargetType != SalesTargetType.Platform)
        {
            var scopeResult = await SalesAcademicScopeValidation.ValidateTargetHasScopeAsync(_academicScope, r.TargetType, r.TargetId, ct);
            if (!scopeResult.IsEligible)
                return ApiResponse<SalesRuleDto>.Fail(scopeResult.Message ?? "هدف البيع غير مربوط بنطاق أكاديمي صالح.", new List<string> { scopeResult.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED" });

            var target = await _targetResolver.ResolveAsync(r.TargetType, r.TargetId, ct);
            if (target == null || !target.IsSaleEligible)
                return ApiResponse<SalesRuleDto>.Fail("هدف البيع غير موجود أو غير مؤهل للبيع.");

            if (r.TargetType is not (SalesTargetType.Teacher or SalesTargetType.VideoType or SalesTargetType.PublicExam)
                && (target.TeacherId == null || target.SubjectId == null))
                return ApiResponse<SalesRuleDto>.Fail("هدف البيع يجب أن يكون مربوطاً بمدرس ومادة قبل استخدامه في البيع.");

            if (r.TargetType == SalesTargetType.SpecificVideo && target.VideoTypeId == null)
                return ApiResponse<SalesRuleDto>.Fail("الفيديو يجب أن يكون له نوع فيديو قبل استخدامه في البيع.");
        }

        var entity = new SalesRule
        {
            TargetType = r.TargetType,
            TargetId = r.TargetId,
            TeacherId = r.TeacherId,
            SubjectId = r.SubjectId,
            GradeLevel = r.GradeLevel,
            VideoTypeId = r.VideoTypeId,
            IsActive = r.IsActive,
            CreatedByUserId = request.ActorId
        };
        _db.SalesRules.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<SalesRuleDto>.Ok(ToDto(entity), "تم حفظ قاعدة البيع.");
    }

    private static SalesRuleDto ToDto(SalesRule x) => new(x.Id, x.TargetType, x.TargetId, x.TeacherId, x.SubjectId, x.GradeLevel, x.VideoTypeId, x.IsActive);
}

public sealed class CreateSalesCouponCommandHandler : IRequestHandler<CreateSalesCouponCommand, ApiResponse<SalesCouponDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public CreateSalesCouponCommandHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<SalesCouponDto>> Handle(CreateSalesCouponCommand request, CancellationToken ct)
    {
        var r = request.Request;
        var normalized = DiscountEngine.NormalizeCode(r.Code);
        if (await _db.SalesCoupons.AnyAsync(x => x.NormalizedCode == normalized, ct))
            return ApiResponse<SalesCouponDto>.Fail("كود الخصم موجود بالفعل.");
        if ((r.OwnerType == SalesOwnerType.Teacher || r.TargetType == SalesTargetType.Teacher) && !r.TeacherId.HasValue)
            return ApiResponse<SalesCouponDto>.Fail("اختيار المدرس مطلوب عند ربط الكوبون بمدرس.");
        if (r.TeacherId.HasValue && !await _db.TeacherProfiles.AnyAsync(x => x.Id == r.TeacherId.Value, ct))
            return ApiResponse<SalesCouponDto>.Fail("المدرس المحدد غير موجود.");

        var scopeResult = await SalesAcademicScopeValidation.ValidateTargetHasScopeAsync(_academicScope, r.TargetType, r.TargetId, ct);
        if (!scopeResult.IsEligible)
            return ApiResponse<SalesCouponDto>.Fail(scopeResult.Message ?? "هدف الكوبون غير مربوط بنطاق أكاديمي صالح.", new List<string> { scopeResult.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED" });
        var ownerScopeResult = await SalesAcademicScopeValidation.ValidateOwnerScopesIfProvidedAsync(_db, r.AcademicScopes, ct);
        if (!ownerScopeResult.IsEligible)
            return ApiResponse<SalesCouponDto>.Fail(ownerScopeResult.Message ?? "نطاق الكوبون الأكاديمي غير صالح.", new List<string> { ownerScopeResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        var entity = new SalesCoupon
        {
            Code = r.Code.Trim(),
            NormalizedCode = normalized,
            Name = r.Name.Trim(),
            DiscountType = r.DiscountType,
            DiscountValue = r.DiscountValue,
            TargetType = r.TargetType,
            TargetId = r.TargetId,
            OwnerType = r.OwnerType,
            TeacherId = r.TeacherId,
            StackingPolicyId = r.StackingPolicyId,
            StartsAt = r.StartsAt,
            ExpiresAt = r.ExpiresAt,
            GlobalUsageLimit = r.GlobalUsageLimit,
            PerStudentUsageLimit = r.PerStudentUsageLimit,
            Status = r.Status,
            CreatedByUserId = request.ActorId
        };
        _db.SalesCoupons.Add(entity);
        await _db.SaveChangesAsync(ct);
        var syncResult = await SalesAcademicScopeValidation.SyncOwnerScopesIfProvidedAsync(
            _db,
            StudentFacingScopeOwnerType.SalesCoupon,
            entity.Id,
            r.AcademicScopes,
            request.ActorId,
            ct);
        if (!syncResult.IsEligible)
            return ApiResponse<SalesCouponDto>.Fail(syncResult.Message ?? "نطاق الكوبون الأكاديمي غير صالح.", new List<string> { syncResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        return ApiResponse<SalesCouponDto>.Ok(ToDto(entity), "تم إنشاء كوبون الخصم.");
    }

    public static SalesCouponDto ToDto(SalesCoupon x, IReadOnlyList<SalesCouponUsageDto>? recentUsages = null) => new(
        x.Id,
        x.Code,
        x.Name,
        x.DiscountType,
        x.DiscountValue,
        x.TargetType,
        x.TargetId,
        x.OwnerType,
        x.TeacherId,
        x.Status,
        x.UsedCount,
        x.StartsAt,
        x.ExpiresAt,
        x.StackingPolicyId,
        x.GlobalUsageLimit,
        x.PerStudentUsageLimit,
        x.DisableReason,
        x.CreatedAt,
        x.UpdatedAt,
        recentUsages ?? Array.Empty<SalesCouponUsageDto>());
}

public sealed class UpdateSalesCouponCommandHandler : IRequestHandler<UpdateSalesCouponCommand, ApiResponse<SalesCouponDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public UpdateSalesCouponCommandHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<SalesCouponDto>> Handle(UpdateSalesCouponCommand request, CancellationToken ct)
    {
        var coupon = await _db.SalesCoupons.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (coupon == null) return ApiResponse<SalesCouponDto>.Fail("الكوبون غير موجود.", new List<string> { "NOT_FOUND" });

        var r = request.Request;
        var normalized = DiscountEngine.NormalizeCode(r.Code);
        if (await _db.SalesCoupons.AnyAsync(x => x.Id != request.Id && x.NormalizedCode == normalized, ct))
            return ApiResponse<SalesCouponDto>.Fail("كود الخصم موجود بالفعل.");
        if ((r.OwnerType == SalesOwnerType.Teacher || r.TargetType == SalesTargetType.Teacher) && !r.TeacherId.HasValue)
            return ApiResponse<SalesCouponDto>.Fail("اختيار المدرس مطلوب عند ربط الكوبون بمدرس.");
        if (r.TeacherId.HasValue && !await _db.TeacherProfiles.AnyAsync(x => x.Id == r.TeacherId.Value, ct))
            return ApiResponse<SalesCouponDto>.Fail("المدرس المحدد غير موجود.");

        var scopeResult = await SalesAcademicScopeValidation.ValidateTargetHasScopeAsync(_academicScope, r.TargetType, r.TargetId, ct);
        if (!scopeResult.IsEligible)
            return ApiResponse<SalesCouponDto>.Fail(scopeResult.Message ?? "هدف الكوبون غير مربوط بنطاق أكاديمي صالح.", new List<string> { scopeResult.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED" });
        var ownerScopeResult = await SalesAcademicScopeValidation.ValidateOwnerScopesIfProvidedAsync(_db, r.AcademicScopes, ct);
        if (!ownerScopeResult.IsEligible)
            return ApiResponse<SalesCouponDto>.Fail(ownerScopeResult.Message ?? "نطاق الكوبون الأكاديمي غير صالح.", new List<string> { ownerScopeResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        coupon.Code = r.Code.Trim();
        coupon.NormalizedCode = normalized;
        coupon.Name = r.Name.Trim();
        coupon.DiscountType = r.DiscountType;
        coupon.DiscountValue = r.DiscountValue;
        coupon.TargetType = r.TargetType;
        coupon.TargetId = r.TargetId;
        coupon.OwnerType = r.OwnerType;
        coupon.TeacherId = r.TeacherId;
        coupon.StackingPolicyId = r.StackingPolicyId;
        coupon.StartsAt = r.StartsAt;
        coupon.ExpiresAt = r.ExpiresAt;
        coupon.GlobalUsageLimit = r.GlobalUsageLimit;
        coupon.PerStudentUsageLimit = r.PerStudentUsageLimit;
        coupon.Status = r.Status;
        if (r.Status != SalesStatus.Disabled)
        {
            coupon.DisableReason = null;
        }
        coupon.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var syncResult = await SalesAcademicScopeValidation.SyncOwnerScopesIfProvidedAsync(
            _db,
            StudentFacingScopeOwnerType.SalesCoupon,
            coupon.Id,
            r.AcademicScopes,
            request.ActorId,
            ct);
        if (!syncResult.IsEligible)
            return ApiResponse<SalesCouponDto>.Fail(syncResult.Message ?? "نطاق الكوبون الأكاديمي غير صالح.", new List<string> { syncResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        return ApiResponse<SalesCouponDto>.Ok(CreateSalesCouponCommandHandler.ToDto(coupon), "تم تحديث كوبون الخصم.");
    }
}

public sealed class DisableSalesCouponCommandHandler : IRequestHandler<DisableSalesCouponCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    public DisableSalesCouponCommandHandler(IAppDbContext db) => _db = db;
    public async Task<ApiResponse<bool>> Handle(DisableSalesCouponCommand request, CancellationToken ct)
    {
        var coupon = await _db.SalesCoupons.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (coupon == null) return ApiResponse<bool>.Fail("الكوبون غير موجود.", new List<string> { "NOT_FOUND" });
        coupon.Status = SalesStatus.Disabled;
        coupon.DisableReason = request.Reason;
        coupon.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "تم تعطيل الكوبون.");
    }
}

public sealed class SaveStackingPolicyCommandHandler : IRequestHandler<SaveStackingPolicyCommand, ApiResponse<StackingPolicyDto>>
{
    private readonly IAppDbContext _db;
    public SaveStackingPolicyCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<StackingPolicyDto>> Handle(SaveStackingPolicyCommand request, CancellationToken ct)
    {
        if (request.Request.IsDefault)
        {
            var defaults = await _db.DiscountStackingPolicies.Where(x => x.IsDefault).ToListAsync(ct);
            foreach (var policy in defaults)
                policy.IsDefault = false;
        }

        var entity = new DiscountStackingPolicy
        {
            Name = request.Request.Name.Trim(),
            NormalizedName = request.Request.Name.Trim().ToUpperInvariant(),
            Mode = request.Request.Mode,
            MaxDiscountPercentage = request.Request.MaxDiscountPercentage,
            MaxDiscountAmount = request.Request.MaxDiscountAmount,
            PriorityJson = string.IsNullOrWhiteSpace(request.Request.PriorityJson) ? "[]" : request.Request.PriorityJson,
            IsDefault = request.Request.IsDefault,
            IsActive = request.Request.IsActive,
            CreatedByUserId = request.ActorId
        };
        _db.DiscountStackingPolicies.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<StackingPolicyDto>.Ok(ToDto(entity), "تم حفظ سياسة دمج الخصومات.");
    }

    public static StackingPolicyDto ToDto(DiscountStackingPolicy x) => new(x.Id, x.Name, x.Mode, x.MaxDiscountPercentage, x.MaxDiscountAmount, x.IsDefault, x.IsActive);
}

public sealed class SavePrintableTemplateCommandHandler : IRequestHandler<SavePrintableTemplateCommand, ApiResponse<PrintableTemplateDto>>
{
    private readonly IAppDbContext _db;
    public SavePrintableTemplateCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PrintableTemplateDto>> Handle(SavePrintableTemplateCommand request, CancellationToken ct)
    {
        var r = request.Request;
        var isUpdate = r.Id.HasValue;
        PrintableCodeTemplate entity;

        if (isUpdate)
        {
            var templateId = r.Id!.Value;
            var existing = await _db.PrintableCodeTemplates.FirstOrDefaultAsync(x => x.Id == templateId, ct);
            if (existing == null)
                return ApiResponse<PrintableTemplateDto>.Fail("قالب الأكواد غير موجود.");

            entity = existing;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            entity = new PrintableCodeTemplate
            {
                CreatedByUserId = request.ActorId
            };
            _db.PrintableCodeTemplates.Add(entity);
        }

        entity.Name = r.Name.Trim();
        entity.WidthMm = r.WidthMm;
        entity.HeightMm = r.HeightMm;
        entity.BackgroundColor = r.BackgroundColor;
        entity.BackgroundImageUrl = r.BackgroundImageUrl;
        entity.LayoutJson = string.IsNullOrWhiteSpace(r.LayoutJson) ? "{}" : r.LayoutJson;
        entity.IsActive = r.IsActive;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<PrintableTemplateDto>.Ok(ToDto(entity), isUpdate ? "تم تحديث قالب الأكواد." : "تم حفظ قالب الأكواد.");
    }

    public static PrintableTemplateDto ToDto(PrintableCodeTemplate x) => new(x.Id, x.Name, x.WidthMm, x.HeightMm, x.BackgroundColor, x.BackgroundImageUrl, x.LayoutJson, x.IsActive);
}

public sealed class CreatePrintableBatchCommandHandler : IRequestHandler<CreatePrintableBatchCommand, ApiResponse<PrintableBatchDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public CreatePrintableBatchCommandHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<PrintableBatchDto>> Handle(CreatePrintableBatchCommand request, CancellationToken ct)
    {
        var r = request.Request;
        if (r.TotalCodes <= 0 || r.TotalCodes > 10000)
            return ApiResponse<PrintableBatchDto>.Fail("عدد الأكواد يجب أن يكون بين 1 و 10000.");
        if ((r.OwnerType == SalesOwnerType.Teacher || r.TargetType == SalesTargetType.Teacher) && !r.TeacherId.HasValue)
            return ApiResponse<PrintableBatchDto>.Fail("اختيار المدرس مطلوب عند ربط دفعة الأكواد بمدرس.");
        if (r.TeacherId.HasValue && !await _db.TeacherProfiles.AnyAsync(x => x.Id == r.TeacherId.Value, ct))
            return ApiResponse<PrintableBatchDto>.Fail("المدرس المحدد غير موجود.");

        var scopeResult = await SalesAcademicScopeValidation.ValidateTargetHasScopeAsync(_academicScope, r.TargetType, r.TargetId, ct);
        if (!scopeResult.IsEligible)
            return ApiResponse<PrintableBatchDto>.Fail(scopeResult.Message ?? "هدف دفعة الأكواد غير مربوط بنطاق أكاديمي صالح.", new List<string> { scopeResult.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED" });
        var ownerScopeResult = await SalesAcademicScopeValidation.ValidateOwnerScopesIfProvidedAsync(_db, r.AcademicScopes, ct);
        if (!ownerScopeResult.IsEligible)
            return ApiResponse<PrintableBatchDto>.Fail(ownerScopeResult.Message ?? "نطاق دفعة الأكواد الأكاديمي غير صالح.", new List<string> { ownerScopeResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        var batch = new PrintableCodeBatch
        {
            Name = r.Name.Trim(),
            Behavior = r.Behavior,
            DiscountType = r.DiscountType,
            DiscountValue = r.DiscountValue,
            CreditAmount = r.CreditAmount,
            TargetType = r.TargetType,
            TargetId = r.TargetId,
            OwnerType = r.OwnerType,
            TeacherId = r.TeacherId,
            TemplateId = r.TemplateId,
            StackingPolicyId = r.StackingPolicyId,
            TotalCodes = r.TotalCodes,
            StartsAt = r.StartsAt,
            ExpiresAt = r.ExpiresAt,
            Status = r.Status,
            CreatedByUserId = request.ActorId
        };

        for (var i = 1; i <= r.TotalCodes; i++)
        {
            var plain = $"NG-{Random.Shared.Next(100000, 999999)}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            batch.Codes.Add(new PrintableSalesCode
            {
                CodePlaintext = plain,
                CodeHash = DiscountEngine.HashCode(plain),
                SerialNumber = i,
                QrPayload = plain,
                UsageLimit = Math.Max(1, r.UsageLimit),
                Status = r.Status == SalesStatus.Active ? SalesStatus.Active : SalesStatus.Draft
            });
        }

        _db.PrintableCodeBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        var syncResult = await SalesAcademicScopeValidation.SyncOwnerScopesIfProvidedAsync(
            _db,
            StudentFacingScopeOwnerType.PrintableCodeBatch,
            batch.Id,
            r.AcademicScopes,
            request.ActorId,
            ct);
        if (!syncResult.IsEligible)
            return ApiResponse<PrintableBatchDto>.Fail(syncResult.Message ?? "نطاق دفعة الأكواد الأكاديمي غير صالح.", new List<string> { syncResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        return ApiResponse<PrintableBatchDto>.Ok(ToDto(batch), "تم إنشاء دفعة الأكواد.");
    }

    public static PrintableBatchDto ToDto(PrintableCodeBatch x) => new(
        x.Id,
        x.Name,
        x.Behavior,
        x.TargetType,
        x.TargetId,
        x.OwnerType,
        x.TeacherId,
        x.TotalCodes,
        x.UsedCount,
        x.Status,
        x.Codes.OrderBy(c => c.SerialNumber).Take(20).Select(c => new PrintableCodeDto(c.Id, c.CodePlaintext ?? string.Empty, c.SerialNumber, c.QrPayload, c.Status)).ToList());
}

internal sealed record PublicExamAvailabilityWindow(DateTime? From, DateTime? Until, string? ValidationError);

internal static class PublicExamAvailabilityRules
{
    public static PublicExamAvailabilityWindow Resolve(DateTime? from, DateTime? until, bool isPublished)
    {
        var normalizedFrom = from.HasValue ? CairoTime.ToUtc(from.Value) : (DateTime?)null;
        var normalizedUntil = until.HasValue ? CairoTime.ToUtc(until.Value) : (DateTime?)null;
        if (normalizedFrom.HasValue && normalizedUntil.HasValue && normalizedUntil <= normalizedFrom)
            return new(normalizedFrom, normalizedUntil, "وقت انتهاء إتاحة الامتحان يجب أن يكون بعد وقت البداية.");
        if (isPublished && normalizedUntil.HasValue && normalizedUntil <= DateTime.UtcNow)
            return new(normalizedFrom, normalizedUntil, "لا يمكن نشر امتحان انتهت فترة إتاحته.");

        return new(normalizedFrom, normalizedUntil, null);
    }
}

public sealed class SavePublicExamProductCommandHandler : IRequestHandler<SavePublicExamProductCommand, ApiResponse<PublicExamProductDto>>
{
    private readonly IAppDbContext _db;
    public SavePublicExamProductCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PublicExamProductDto>> Handle(SavePublicExamProductCommand request, CancellationToken ct)
    {
        var availability = PublicExamAvailabilityRules.Resolve(
            request.Request.AvailableFrom,
            request.Request.AvailableUntil,
            request.Request.IsPublished);
        if (availability.ValidationError != null)
            return ApiResponse<PublicExamProductDto>.Fail(availability.ValidationError);

        var exam = await _db.Exams.FirstOrDefaultAsync(x => x.Id == request.Request.ExamId, ct);
        if (exam == null) return ApiResponse<PublicExamProductDto>.Fail("الامتحان غير موجود.");
        var requestedScopeResult = await SalesAcademicScopeValidation.ValidatePublicExamScopesOrLegacyAsync(
            _db,
            request.Request.IsPlatformWide,
            request.Request.GradeLevel,
            request.Request.SubjectId,
            request.Request.AcademicScopes,
            ct);
        if (!requestedScopeResult.IsEligible)
            return ApiResponse<PublicExamProductDto>.Fail(requestedScopeResult.Message ?? "نطاق الامتحان العام الأكاديمي غير صالح.", new List<string> { requestedScopeResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        var existing = await _db.PublicExamProducts.FirstOrDefaultAsync(x => x.ExamId == request.Request.ExamId, ct);
        if (existing == null)
        {
            existing = new PublicExamProduct { ExamId = request.Request.ExamId, CreatedByUserId = request.ActorId };
            _db.PublicExamProducts.Add(existing);
        }

        existing.Slug = request.Request.Slug.Trim().ToLowerInvariant();
        existing.IsPublished = request.Request.IsPublished;
        existing.IsPaid = request.Request.IsPaid;
        existing.Price = request.Request.IsPaid ? request.Request.Price : 0;
        existing.TeacherId = request.Request.TeacherId;
        existing.SubjectId = request.Request.SubjectId;
        existing.GradeLevel = request.Request.GradeLevel;
        existing.IsPlatformWide = request.Request.IsPlatformWide;
        existing.AvailableFrom = availability.From;
        existing.AvailableUntil = availability.Until;
        existing.DisabledAt = null;
        existing.DisabledByUserId = null;
        existing.DisableReason = null;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var scopeResult = await SalesAcademicScopeValidation.SyncOwnerScopesOrLegacyPublicExamAsync(_db, existing, request.Request.AcademicScopes, request.ActorId, ct);
        if (!scopeResult.IsEligible)
            return ApiResponse<PublicExamProductDto>.Fail(scopeResult.Message ?? "نطاق الامتحان العام الأكاديمي غير صالح.", new List<string> { scopeResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        return ApiResponse<PublicExamProductDto>.Ok(await ToPublicExamProductDtoAsync(_db, existing, exam.Title, ct), "تم حفظ الامتحان العام.");
    }

    internal static async Task<PublicExamProductDto> ToPublicExamProductDtoAsync(IAppDbContext db, PublicExamProduct product, string examTitle, CancellationToken ct)
    {
        var scopes = await db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == StudentFacingScopeOwnerType.PublicExamProduct && x.OwnerId == product.Id)
            .ToListAsync(ct);
        var archiveState = await db.Exams.AsNoTracking()
            .Where(exam => exam.Id == product.ExamId)
            .Select(exam => new { exam.ArchiveMode, exam.ArchivedAt })
            .FirstAsync(ct);

        return new PublicExamProductDto(
            product.Id,
            product.ExamId,
            examTitle,
            product.Slug,
            product.IsPublished,
            product.IsPaid,
            product.Price,
            product.TeacherId,
            product.SubjectId,
            product.GradeLevel,
            product.IsPlatformWide,
            product.AvailableFrom,
            product.AvailableUntil,
            product.DisabledAt,
            AcademicScopeService.ToScopeSummaries(scopes),
            archiveState.ArchiveMode,
            archiveState.ArchivedAt);
    }
}

public sealed class CreatePublicExamProductCommandHandler : IRequestHandler<CreatePublicExamProductCommand, ApiResponse<PublicExamProductDto>>
{
    private readonly IAppDbContext _db;
    public CreatePublicExamProductCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PublicExamProductDto>> Handle(CreatePublicExamProductCommand request, CancellationToken ct)
    {
        var r = request.Request;
        var availability = PublicExamAvailabilityRules.Resolve(r.AvailableFrom, r.AvailableUntil, r.IsPublished);
        if (string.IsNullOrWhiteSpace(r.Title))
            return ApiResponse<PublicExamProductDto>.Fail("اسم الامتحان مطلوب.");
        if (string.IsNullOrWhiteSpace(r.Slug))
            return ApiResponse<PublicExamProductDto>.Fail("رابط الامتحان مطلوب.");
        if (r.PassingScore > r.TotalScore)
            return ApiResponse<PublicExamProductDto>.Fail("درجة النجاح لا يمكن أن تكون أكبر من الدرجة النهائية.");
        if (r.IsPaid && r.Price < 0)
            return ApiResponse<PublicExamProductDto>.Fail("سعر الامتحان غير صحيح.");
        if (availability.ValidationError != null)
            return ApiResponse<PublicExamProductDto>.Fail(availability.ValidationError);
        var requestedTeacherId = r.TeacherId == Guid.Empty ? null : r.TeacherId;
        if (r.SubjectId == Guid.Empty || !await _db.Subjects.AnyAsync(x => x.Id == r.SubjectId, ct))
            return ApiResponse<PublicExamProductDto>.Fail("المادة المحددة غير موجودة.");
        if (requestedTeacherId.HasValue && !await _db.TeacherProfiles.AnyAsync(x => x.Id == requestedTeacherId.Value, ct))
            return ApiResponse<PublicExamProductDto>.Fail("المدرس المحدد غير موجود.");
        var examOwnerTeacherId = requestedTeacherId ?? await _db.TeacherSubjects
            .Where(x => x.SubjectId == r.SubjectId)
            .OrderBy(x => x.TeacherId)
            .Select(x => (Guid?)x.TeacherId)
            .FirstOrDefaultAsync(ct);
        if (!examOwnerTeacherId.HasValue)
            return ApiResponse<PublicExamProductDto>.Fail("لا يوجد مدرس مرتبط بالمادة المحددة لاستخدامه كمالك داخلي للامتحان.");
        if (await _db.PublicExamProducts.AnyAsync(x => x.Slug == r.Slug.Trim(), ct))
            return ApiResponse<PublicExamProductDto>.Fail("رابط الامتحان مستخدم بالفعل.");
        var requestedScopeResult = await SalesAcademicScopeValidation.ValidatePublicExamScopesOrLegacyAsync(
            _db,
            false,
            r.GradeLevel,
            r.SubjectId,
            r.AcademicScopes,
            ct);
        if (!requestedScopeResult.IsEligible)
            return ApiResponse<PublicExamProductDto>.Fail(requestedScopeResult.Message ?? "نطاق الامتحان العام الأكاديمي غير صالح.", new List<string> { requestedScopeResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        var exam = new Exam
        {
            Title = r.Title.Trim(),
            Description = r.Description?.Trim() ?? string.Empty,
            PassingScore = r.PassingScore,
            TotalScore = r.TotalScore,
            DurationMinutes = r.DurationMinutes,
            IsMandatory = false,
            IsRandomized = r.IsRandomized,
            CreatedByTeacherId = examOwnerTeacherId.Value
        };

        var product = new PublicExamProduct
        {
            Exam = exam,
            Slug = r.Slug.Trim(),
            IsPublished = r.IsPublished,
            IsPaid = r.IsPaid,
            Price = r.IsPaid ? r.Price : 0,
            TeacherId = requestedTeacherId,
            SubjectId = r.SubjectId,
            GradeLevel = r.GradeLevel,
            IsPlatformWide = false,
            AvailableFrom = availability.From,
            AvailableUntil = availability.Until,
            CreatedByUserId = request.ActorId
        };

        _db.Exams.Add(exam);
        _db.PublicExamProducts.Add(product);
        await _db.SaveChangesAsync(ct);
        var scopeResult = await SalesAcademicScopeValidation.SyncOwnerScopesOrLegacyPublicExamAsync(_db, product, r.AcademicScopes, request.ActorId, ct);
        if (!scopeResult.IsEligible)
            return ApiResponse<PublicExamProductDto>.Fail(scopeResult.Message ?? "نطاق الامتحان العام الأكاديمي غير صالح.", new List<string> { scopeResult.ErrorCode ?? "ACADEMIC_SCOPE_REQUIRED" });

        return ApiResponse<PublicExamProductDto>.Ok(
            await SavePublicExamProductCommandHandler.ToPublicExamProductDtoAsync(_db, product, exam.Title, ct),
            "تم إنشاء الامتحان العام.");
    }
}

public sealed class DisablePublicExamProductCommandHandler : IRequestHandler<DisablePublicExamProductCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    public DisablePublicExamProductCommandHandler(IAppDbContext db) => _db = db;
    public async Task<ApiResponse<bool>> Handle(DisablePublicExamProductCommand request, CancellationToken ct)
    {
        var product = await _db.PublicExamProducts.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (product == null) return ApiResponse<bool>.Fail("الامتحان العام غير موجود.", new List<string> { "NOT_FOUND" });
        product.IsPublished = false;
        product.DisabledAt = DateTime.UtcNow;
        product.DisabledByUserId = request.ActorId;
        product.DisableReason = request.Reason;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "تم تعطيل الامتحان العام مع الحفاظ على المحاولات القديمة.");
    }
}

internal static class SalesAcademicScopeValidation
{
    public static async Task<AcademicScopeCheckResult> ValidateOwnerScopesIfProvidedAsync(
        IAppDbContext db,
        IReadOnlyList<AcademicScopeDto>? scopes,
        CancellationToken ct)
    {
        if (scopes == null)
            return AcademicScopeCheckResult.Eligible();

        return await ValidateScopesAsync(db, scopes, ct);
    }

    public static Task<AcademicScopeCheckResult> ValidatePublicExamScopesOrLegacyAsync(
        IAppDbContext db,
        bool isPlatformWide,
        string? gradeLevel,
        Guid? subjectId,
        IReadOnlyList<AcademicScopeDto>? scopes,
        CancellationToken ct)
    {
        var requestedScopes = scopes ?? BuildLegacyPublicExamScopes(isPlatformWide, gradeLevel, subjectId);
        return ValidateScopesAsync(db, requestedScopes, ct);
    }

    private static async Task<AcademicScopeCheckResult> ValidateScopesAsync(
        IAppDbContext db,
        IReadOnlyList<AcademicScopeDto> scopes,
        CancellationToken ct)
    {
        var service = new AcademicScopeService(db);
        var result = await service.ValidateScopeDtosAsync(scopes, ct);
        return result.IsValid
            ? AcademicScopeCheckResult.Eligible()
            : AcademicScopeCheckResult.Denied(result.ErrorCode ?? "ACADEMIC_SCOPE_INVALID", result.Message ?? "نطاق أكاديمي غير صالح.");
    }

    public static async Task<AcademicScopeCheckResult> SyncOwnerScopesIfProvidedAsync(
        IAppDbContext db,
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId,
        IReadOnlyList<AcademicScopeDto>? scopes,
        Guid actorId,
        CancellationToken ct)
    {
        if (scopes == null)
            return AcademicScopeCheckResult.Eligible();

        var service = new AcademicScopeService(db);
        var result = await service.SyncOwnerScopesAsync(ownerType, ownerId, scopes, actorId, ct);
        return result.IsValid
            ? AcademicScopeCheckResult.Eligible()
            : AcademicScopeCheckResult.Denied(result.ErrorCode ?? "ACADEMIC_SCOPE_INVALID", result.Message ?? "نطاق أكاديمي غير صالح.");
    }

    public static async Task<AcademicScopeCheckResult> SyncOwnerScopesOrLegacyPublicExamAsync(
        IAppDbContext db,
        PublicExamProduct product,
        IReadOnlyList<AcademicScopeDto>? scopes,
        Guid actorId,
        CancellationToken ct)
    {
        var requestedScopes = scopes ?? BuildLegacyPublicExamScopes(product.IsPlatformWide, product.GradeLevel, product.SubjectId);
        var service = new AcademicScopeService(db);
        var result = await service.SyncOwnerScopesAsync(StudentFacingScopeOwnerType.PublicExamProduct, product.Id, requestedScopes, actorId, ct);
        return result.IsValid
            ? AcademicScopeCheckResult.Eligible()
            : AcademicScopeCheckResult.Denied(result.ErrorCode ?? "ACADEMIC_SCOPE_INVALID", result.Message ?? "نطاق الامتحان العام الأكاديمي غير صالح.");
    }

    public static async Task<AcademicScopeCheckResult> ValidateTargetHasScopeAsync(
        IAcademicScopeService? academicScope,
        SalesTargetType targetType,
        Guid? targetId,
        CancellationToken ct)
    {
        if (academicScope == null || targetType == SalesTargetType.Platform || targetType == SalesTargetType.VideoType)
            return AcademicScopeCheckResult.Eligible();

        var ownerType = targetType switch
        {
            SalesTargetType.Package => StudentFacingScopeOwnerType.Package,
            SalesTargetType.Term => StudentFacingScopeOwnerType.Term,
            SalesTargetType.ContentSection => StudentFacingScopeOwnerType.ContentSection,
            SalesTargetType.Lesson => StudentFacingScopeOwnerType.Lesson,
            SalesTargetType.SpecificVideo => StudentFacingScopeOwnerType.LessonVideo,
            SalesTargetType.PublicExam => StudentFacingScopeOwnerType.PublicExamProduct,
            SalesTargetType.Teacher => StudentFacingScopeOwnerType.Teacher,
            _ => (StudentFacingScopeOwnerType?)null
        };

        if (!ownerType.HasValue)
            return AcademicScopeCheckResult.Eligible();

        if (!targetId.HasValue)
            return AcademicScopeCheckResult.Denied("ACADEMIC_SCOPE_TARGET_UNSCOPED", "هدف البيع يجب أن يكون مربوطا بنطاق أكاديمي صالح أو نطاق عام صريح.");

        return await academicScope.ValidateTargetHasScopeAsync(ownerType.Value, targetId.Value, ct);
    }

    private static IReadOnlyList<AcademicScopeDto> BuildLegacyPublicExamScopes(bool isPlatformWide, string? gradeLevel, Guid? subjectId)
    {
        if (isPlatformWide)
            return [new AcademicScopeDto(AcademicScopeLevel.PlatformWide)];

        if (subjectId.HasValue &&
            AcademicScopeService.TryNormalizeGradeAlias(gradeLevel, out var exactGrade))
        {
            return [new AcademicScopeDto(AcademicScopeLevel.Exact, EducationStage.Secondary, exactGrade, subjectId.Value)];
        }

        if (AcademicScopeService.TryNormalizeGradeAlias(gradeLevel, out var grade))
            return [new AcademicScopeDto(AcademicScopeLevel.GradeAllSubjects, EducationStage.Secondary, grade)];

        return [];
    }
}
