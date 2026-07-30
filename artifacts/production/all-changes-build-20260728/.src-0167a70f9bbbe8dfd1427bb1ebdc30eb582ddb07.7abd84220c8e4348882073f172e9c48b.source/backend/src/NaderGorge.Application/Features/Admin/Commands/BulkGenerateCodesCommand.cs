using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Services;
using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Application.Features.Admin.Commands;

/// <summary>
/// Phase 3: Expanded code generation supporting all 6 code types.
/// </summary>
public record BulkGenerateCodesCommand(
    string GroupName,
    CodeType CodeType,
    int Count,
    int CodeLength,
    Guid AdminId,
    // Target references (one required depending on CodeType)
    Guid? PackageId = null,
    Guid? TermId = null,
    Guid? ContentSectionId = null,
    Guid? LessonId = null,
    Guid? ExamId = null,
    Guid? PublicExamProductId = null,
    Guid? VideoTypeId = null,
    bool IncludeFutureVideos = true,
    // Video targets (for CodeType.Video)
    List<Guid>? VideoTargetIds = null,
    // Balance (for CodeType.Balance)
    decimal? BalanceAmount = null,
    // Optional admin-selected teacher context. Null means platform-wide for targets that do not imply a teacher.
    Guid? TeacherId = null,
    // Optional
    decimal? DiscountPercentage = null,
    SalesOwnerType? RevenueOwner = null,
    TeacherAllocationMode? RevenueAllocationMode = null,
    decimal? RevenueAllocationValue = null,
    CodeAccountingTiming AccountingTiming = CodeAccountingTiming.OnActivation,
    DateTime? ExpiresAt = null,
    bool ExpireActivatedAccess = true,
    IReadOnlyList<AcademicScopeDto>? AcademicScopes = null
) : IRequest<ApiResponse<BulkGenerateCodesResponse>>;

public record BulkGenerateCodesResponse(Guid CodeGroupId, int CodesGenerated, List<string> Codes);

internal sealed record CodeTargetPricing(
    decimal Price,
    SalesTargetType TargetType,
    Guid TargetId,
    string ContentName
);

public class BulkGenerateCodesCommandHandler : IRequestHandler<BulkGenerateCodesCommand, ApiResponse<BulkGenerateCodesResponse>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAcademicScopeService? _academicScope;

    public BulkGenerateCodesCommandHandler(IAppDbContext db, IAuditService audit, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _audit = audit;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<BulkGenerateCodesResponse>> Handle(BulkGenerateCodesCommand request, CancellationToken ct)
    {
        var expiresAt = request.ExpiresAt.HasValue ? CairoTime.ToUtc(request.ExpiresAt.Value) : (DateTime?)null;

        if (request.Count <= 0 || request.Count > 10_000)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Count must be between 1 and 10,000");

        if (request.CodeLength < 6 || request.CodeLength > 20)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Code length must be between 6 and 20");

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("تاريخ انتهاء الأكواد يجب أن يكون في المستقبل.");

        // Validate target based on CodeType
        var validationError = ValidateTargets(request);
        if (validationError != null)
            return ApiResponse<BulkGenerateCodesResponse>.Fail(validationError);

        // Resolve user role
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == request.AdminId, ct);

        if (user == null)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("User not found.");

        var isTeacher = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher);
        if (isTeacher)
        {
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Unauthorized: Teachers are not allowed to generate codes.");
        }

        var permissionsList = new List<string>();
        foreach (var ur in user.UserRoles)
        {
            if (ur.Role != null && !string.IsNullOrEmpty(ur.Role.PermissionsJson))
            {
                try
                {
                    var perms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ur.Role.PermissionsJson);
                    if (perms != null)
                        permissionsList.AddRange(perms);
                }
                catch { }
            }
        }

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin);
        if (!isAdmin && !permissionsList.Contains("codes.manage"))
        {
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Unauthorized: You do not have permission to manage codes.");
        }

        Guid? explicitTeacherId = null;
        if (request.TeacherId.HasValue && request.TeacherId.Value != Guid.Empty)
        {
            var teacherExists = await _db.TeacherProfiles.AnyAsync(tp => tp.Id == request.TeacherId.Value, ct);
            if (!teacherExists)
                return ApiResponse<BulkGenerateCodesResponse>.Fail("Selected teacher was not found.");

            explicitTeacherId = request.TeacherId.Value;
        }

        Guid? targetTeacherId = null;

        // Resolve teacher from the selected target when possible.
        if (request.PackageId.HasValue)
        {
            var pkg = await _db.Packages.FindAsync(new object[] { request.PackageId.Value }, ct);
            targetTeacherId = pkg?.TeacherId;
        }
        else if (request.TermId.HasValue)
        {
            var term = await _db.Terms.Include(t => t.Package).FirstOrDefaultAsync(t => t.Id == request.TermId.Value, ct);
            targetTeacherId = term?.Package?.TeacherId;
        }
        else if (request.ContentSectionId.HasValue)
        {
            var sec = await _db.ContentSections.Include(s => s.Term).ThenInclude(t => t.Package).FirstOrDefaultAsync(s => s.Id == request.ContentSectionId.Value, ct);
            targetTeacherId = sec?.Term?.Package?.TeacherId;
        }
        else if (request.LessonId.HasValue)
        {
            var les = await _db.Lessons.Include(l => l.ContentSection).ThenInclude(s => s.Term).ThenInclude(t => t.Package).FirstOrDefaultAsync(l => l.Id == request.LessonId.Value, ct);
            targetTeacherId = les?.ContentSection?.Term?.Package?.TeacherId;
        }
        else if (request.ExamId.HasValue)
        {
            var exam = await _db.Exams.FindAsync(new object[] { request.ExamId.Value }, ct);
            targetTeacherId = exam?.CreatedByTeacherId;
        }
        else if (request.CodeType == CodeType.Video && request.VideoTargetIds != null && request.VideoTargetIds.Any())
        {
            var vid = await _db.LessonVideos.Include(v => v.Lesson).ThenInclude(l => l.ContentSection).ThenInclude(s => s.Term).ThenInclude(t => t.Package).FirstOrDefaultAsync(v => v.Id == request.VideoTargetIds.First(), ct);
            targetTeacherId = vid?.Lesson?.ContentSection?.Term?.Package?.TeacherId;
        }

        if (request.CodeType == CodeType.Video && request.VideoTypeId.HasValue)
        {
            var videoTypeExists = await _db.VideoTypes.AnyAsync(vt => vt.Id == request.VideoTypeId.Value && vt.IsActive, ct);
            if (!videoTypeExists)
                return ApiResponse<BulkGenerateCodesResponse>.Fail("Selected video type was not found or is inactive.");
        }

        Guid? publicExamId = null;
        if (request.PublicExamProductId.HasValue)
        {
            var publicExam = await _db.PublicExamProducts
                .FirstOrDefaultAsync(exam => exam.Id == request.PublicExamProductId.Value && exam.DisabledAt == null, ct);
            if (publicExam == null)
                return ApiResponse<BulkGenerateCodesResponse>.Fail("الامتحان العام المحدد غير موجود أو متوقف.");

            publicExamId = publicExam.ExamId;
            targetTeacherId ??= publicExam.TeacherId;
        }

        if (explicitTeacherId.HasValue && targetTeacherId.HasValue && explicitTeacherId != targetTeacherId)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Selected teacher does not match the selected content.");

        var groupTeacherId = targetTeacherId ?? explicitTeacherId;
        var targetPricing = await ResolveTargetPricingAsync(request, ct);

        var scopeValidation = await ValidateAcademicTargetsHaveScopeAsync(request, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse<BulkGenerateCodesResponse>.Fail(scopeValidation.Message ?? "هدف الكود غير مربوط بنطاق أكاديمي صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED" });

        if (request.AcademicScopes != null)
        {
            var ownerScopeValidation = await new AcademicScopeService(_db).ValidateScopeDtosAsync(request.AcademicScopes, ct);
            if (!ownerScopeValidation.IsValid)
                return ApiResponse<BulkGenerateCodesResponse>.Fail(
                    ownerScopeValidation.Message ?? "نطاق مجموعة الأكواد الأكاديمي غير صالح.",
                    new List<string> { ownerScopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });
        }

        if (request.RevenueOwner == SalesOwnerType.Teacher && !groupTeacherId.HasValue)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("يجب اختيار مدرس عند جعل الربح تابعاً للمدرس.");

        if (request.RevenueAllocationValue.HasValue && request.RevenueAllocationValue.Value < 0)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("قيمة توزيع الربح لا يمكن أن تكون سالبة.");

        if (request.RevenueAllocationMode == TeacherAllocationMode.Percentage
            && request.RevenueAllocationValue.HasValue
            && request.RevenueAllocationValue.Value > 100)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("النسبة لا يمكن أن تزيد عن 100%.");

        var videoTargetIds = request.VideoTargetIds?.Distinct().ToList() ?? new List<Guid>();
        if (request.CodeType == CodeType.Video && request.VideoTypeId.HasValue && !request.IncludeFutureVideos)
        {
            var snapshotTargets = await _db.LessonVideos
                .Where(video => video.IsActive && video.VideoTypeId == request.VideoTypeId.Value)
                .Where(video => !request.PackageId.HasValue || video.Lesson.ContentSection.Term.PackageId == request.PackageId.Value)
                .Where(video => !request.TermId.HasValue || video.Lesson.ContentSection.TermId == request.TermId.Value)
                .Where(video => !request.ContentSectionId.HasValue || video.Lesson.ContentSectionId == request.ContentSectionId.Value)
                .Where(video => !request.LessonId.HasValue || video.LessonId == request.LessonId.Value)
                .Select(video => video.Id)
                .ToListAsync(ct);

            if (snapshotTargets.Count == 0)
                return ApiResponse<BulkGenerateCodesResponse>.Fail("لا توجد فيديوهات متاحة حالياً من نوع الفيديو والنطاق المحددين.");

            videoTargetIds.AddRange(snapshotTargets);
            videoTargetIds = videoTargetIds.Distinct().ToList();
        }

        // Create code group
        var group = new CodeGroup
        {
            Id = Guid.NewGuid(),
            Name = request.GroupName ?? $"Batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            TotalCodes = request.Count,
            CodeType = request.CodeType,
            PackageId = request.PackageId,
            TermId = request.TermId,
            ContentSectionId = request.ContentSectionId,
            LessonId = request.LessonId,
            ExamId = publicExamId ?? request.ExamId,
            PublicExamProductId = request.PublicExamProductId,
            VideoTypeId = request.VideoTypeId,
            IncludeFutureVideos = request.IncludeFutureVideos,
            BalanceAmount = request.BalanceAmount,
            DiscountPercentage = request.DiscountPercentage,
            RevenueOwner = request.RevenueOwner,
            RevenueAllocationMode = request.RevenueAllocationMode,
            RevenueAllocationValue = request.RevenueAllocationValue,
            AccountingTiming = request.AccountingTiming,
            ExpiresAt = expiresAt,
            ExpireActivatedAccess = request.ExpireActivatedAccess,
            CreatedByUserId = request.AdminId,
            TeacherId = groupTeacherId
        };
        _db.CodeGroups.Add(group);
        // Keep the old timing field for backwards compatibility, while the finance
        // center owns the explicit trigger. "Immediate" is now a delivery-billed
        // batch and is intentionally not credited until an admin confirms delivery.
        _db.CodeGroupFinancialTerms.Add(new CodeGroupFinancialTerms
        {
            Id = Guid.NewGuid(),
            CodeGroupId = group.Id,
            Trigger = request.AccountingTiming == CodeAccountingTiming.Immediate
                ? TeacherAgreementTrigger.CodeDelivery
                : TeacherAgreementTrigger.CodeActivation,
            UpdatedByUserId = request.AdminId
        });

        // Add video targets if Video code type
        if (request.CodeType == CodeType.Video && videoTargetIds.Count > 0)
        {
            foreach (var videoId in videoTargetIds)
            {
                _db.CodeVideoTargets.Add(new CodeVideoTarget
                {
                    CodeGroupId = group.Id,
                    LessonVideoId = videoId
                });
            }
        }

        // Generate codes
        var maxSerial = await _db.AccessCodes.MaxAsync(c => (long?)c.SerialNumber, ct) ?? 10000000;
        var codes = new List<AccessCode>(request.Count);
        var plaintexts = new List<string>(request.Count);

        for (int i = 0; i < request.Count; i++)
        {
            var plaintext = GenerateSecureCode(request.CodeLength);
            var hash = HashCode(plaintext);
            maxSerial++;

            codes.Add(new AccessCode
            {
                Id = Guid.NewGuid(),
                CodeHash = hash,
                CodePlaintext = plaintext,
                CodeGroupId = group.Id,
                IsConsumed = false,
                ExpiresAt = expiresAt,
                SerialNumber = maxSerial
            });

            plaintexts.Add(plaintext);
        }

        _db.AccessCodes.AddRange(codes);

        if (request.AcademicScopes != null)
        {
            foreach (var scope in request.AcademicScopes)
            {
                _db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
                {
                    OwnerType = StudentFacingScopeOwnerType.CodeGroup,
                    OwnerId = group.Id,
                    ScopeLevel = scope.ScopeLevel,
                    EducationStage = scope.EducationStage,
                    GradeLevel = scope.GradeLevel,
                    SubjectId = scope.SubjectId,
                    CreatedByUserId = request.AdminId
                });
            }
        }

        var codeGroupCreatedEvent = new OutboxEvent
        {
            Type = "CodeGroupCreated",
            TargetUserId = request.AdminId.ToString(),
            TargetGroup = null,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                codeGroupId = group.Id,
                name = group.Name,
                codeType = group.CodeType.ToString(),
                totalCodes = group.TotalCodes,
                createdAt = DateTime.UtcNow
            })
        };
        _db.OutboxEvents.Add(codeGroupCreatedEvent);

        var codeGroupExportReadyEvent = new OutboxEvent
        {
            Type = "CodeGroupExportReady",
            TargetUserId = request.AdminId.ToString(),
            TargetGroup = null,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                codeGroupId = group.Id,
                name = group.Name,
                totalCodes = group.TotalCodes,
                exportStatus = "Ready"
            })
        };
        _db.OutboxEvents.Add(codeGroupExportReadyEvent);

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action: "BulkGenerateCodes",
            entityType: "CodeGroup",
            entityId: group.Id,
            userId: request.AdminId,
            newValues: new { Count = request.Count, CodeType = request.CodeType.ToString() }
        );

        return ApiResponse<BulkGenerateCodesResponse>.Ok(
            new BulkGenerateCodesResponse(group.Id, request.Count, plaintexts));
    }

    private async Task<AcademicScopeCheckResult> ValidateAcademicTargetsHaveScopeAsync(BulkGenerateCodesCommand request, CancellationToken ct)
    {
        if (_academicScope == null || request.CodeType == CodeType.Balance)
            return AcademicScopeCheckResult.Eligible();

        var targets = new List<(StudentFacingScopeOwnerType OwnerType, Guid OwnerId)>();
        switch (request.CodeType)
        {
            case CodeType.Package when request.PackageId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Package, request.PackageId.Value));
                break;
            case CodeType.Term when request.TermId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Term, request.TermId.Value));
                break;
            case CodeType.Month when request.ContentSectionId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.ContentSection, request.ContentSectionId.Value));
                break;
            case CodeType.Lesson when request.LessonId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Lesson, request.LessonId.Value));
                break;
            case CodeType.Exam when request.PublicExamProductId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.PublicExamProduct, request.PublicExamProductId.Value));
                break;
            case CodeType.Exam when request.ExamId.HasValue:
                targets.Add((StudentFacingScopeOwnerType.Exam, request.ExamId.Value));
                break;
            case CodeType.Video:
                if (request.LessonId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.Lesson, request.LessonId.Value));
                else if (request.ContentSectionId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.ContentSection, request.ContentSectionId.Value));
                else if (request.TermId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.Term, request.TermId.Value));
                else if (request.PackageId.HasValue)
                    targets.Add((StudentFacingScopeOwnerType.Package, request.PackageId.Value));

                if (request.VideoTargetIds != null)
                    targets.AddRange(request.VideoTargetIds.Select(videoId => (StudentFacingScopeOwnerType.LessonVideo, videoId)));
                break;
        }

        if (targets.Count == 0)
            return AcademicScopeCheckResult.Denied("ACADEMIC_SCOPE_TARGET_UNSCOPED", "هدف الكود يجب أن يكون مربوطا بنطاق أكاديمي صالح أو نطاق عام صريح.");

        foreach (var (ownerType, ownerId) in targets)
        {
            var result = await _academicScope.ValidateTargetHasScopeAsync(ownerType, ownerId, ct);
            if (!result.IsEligible)
                return result;
        }

        return AcademicScopeCheckResult.Eligible();
    }

    private static string? ValidateTargets(BulkGenerateCodesCommand request)
    {
        return request.CodeType switch
        {
            CodeType.Package when request.PackageId == null => "PackageId is required for Package codes",
            CodeType.Term when request.TermId == null => "TermId is required for Term codes",
            CodeType.Month when request.ContentSectionId == null => "ContentSectionId is required for Month codes",
            CodeType.Lesson when request.LessonId == null => "LessonId is required for Lesson codes",
            CodeType.Exam when request.ExamId == null && request.PublicExamProductId == null => "ExamId or PublicExamProductId is required for Exam codes",
            CodeType.Video when !request.VideoTypeId.HasValue && (request.VideoTargetIds == null || request.VideoTargetIds.Count == 0) => "VideoTypeId or VideoTargetIds are required for Video codes",
            CodeType.Balance when (request.BalanceAmount == null || request.BalanceAmount <= 0) => "BalanceAmount must be > 0 for Balance codes",
            _ => null
        };
    }

    private async Task<CodeTargetPricing> ResolveTargetPricingAsync(BulkGenerateCodesCommand request, CancellationToken ct)
    {
        if (request.PackageId.HasValue)
        {
            var item = await _db.Packages.FirstOrDefaultAsync(x => x.Id == request.PackageId.Value, ct);
            if (item != null) return new(item.Price, SalesTargetType.Package, item.Id, item.Name);
        }

        if (request.TermId.HasValue)
        {
            var item = await _db.Terms.FirstOrDefaultAsync(x => x.Id == request.TermId.Value, ct);
            if (item != null) return new(item.Price, SalesTargetType.Term, item.Id, item.Title);
        }

        if (request.ContentSectionId.HasValue)
        {
            var item = await _db.ContentSections.FirstOrDefaultAsync(x => x.Id == request.ContentSectionId.Value, ct);
            if (item != null) return new(item.Price, SalesTargetType.ContentSection, item.Id, item.Title);
        }

        if (request.LessonId.HasValue)
        {
            var item = await _db.Lessons.FirstOrDefaultAsync(x => x.Id == request.LessonId.Value, ct);
            if (item != null) return new(item.Price, SalesTargetType.Lesson, item.Id, item.Title);
        }

        if (request.PublicExamProductId.HasValue)
        {
            var publicExam = await _db.PublicExamProducts
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == request.PublicExamProductId.Value, ct);
            if (publicExam != null)
                return new(publicExam.Price, SalesTargetType.PublicExam, publicExam.Id, publicExam.Exam.Title);
        }

        if (request.ExamId.HasValue)
        {
            var publicExam = await _db.PublicExamProducts
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.ExamId == request.ExamId.Value || x.Id == request.ExamId.Value, ct);

            if (publicExam != null)
                return new(publicExam.Price, SalesTargetType.PublicExam, publicExam.Id, publicExam.Exam.Title);
        }

        return new(0m, SalesTargetType.Platform, request.PackageId ?? request.TermId ?? request.ContentSectionId ?? request.LessonId ?? request.ExamId ?? request.VideoTypeId ?? Guid.Empty, request.GroupName);
    }

    private async Task RecordImmediateAccountingAsync(
        CodeGroup group,
        Guid? teacherId,
        CodeTargetPricing targetPricing,
        int count,
        CancellationToken ct)
    {
        var grossTotal = Math.Max(0m, targetPricing.Price) * count;
        var teacher = teacherId.HasValue
            ? await _db.TeacherProfiles.FirstOrDefaultAsync(x => x.Id == teacherId.Value, ct)
            : null;

        var (teacherShare, platformShare, allocationMode, allocationValue, basisAmount) =
            CalculateShares(grossTotal, teacher, group.RevenueOwner, group.RevenueAllocationMode, group.RevenueAllocationValue);

        await new TeacherAccountingService(_db).RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.AccessCodeGeneration,
            group.Id,
            null,
            targetPricing.TargetType,
            targetPricing.TargetId == Guid.Empty ? group.Id : targetPricing.TargetId,
            grossTotal,
            0m,
            grossTotal,
            0m,
            platformShare,
            $"access-code-group-immediate:{group.Id}",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                codeGroupId = group.Id,
                group.Name,
                group.CodeType,
                count,
                accountingTiming = group.AccountingTiming.ToString(),
                revenueOwner = group.RevenueOwner?.ToString()
            }),
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            teacher != null && teacherShare > 0m
                ? new[]
                {
                    new TeacherFinancialAllocationInput(
                        teacher.Id,
                        allocationMode,
                        allocationValue,
                        basisAmount,
                        teacherShare,
                        platformShare,
                        null,
                        null,
                        group.Name,
                        null)
                }
                : Array.Empty<TeacherFinancialAllocationInput>()), ct);

        group.AccountingRecordedAt = DateTime.UtcNow;
    }

    internal static (decimal TeacherShare, decimal PlatformShare, TeacherAllocationMode AllocationMode, decimal AllocationValue, decimal BasisAmount) CalculateShares(
        decimal grossAmount,
        TeacherProfile? teacher,
        SalesOwnerType? revenueOwner,
        TeacherAllocationMode? allocationMode,
        decimal? allocationValue)
    {
        if (grossAmount <= 0m)
            return (0m, 0m, TeacherAllocationMode.CommissionRate, teacher?.CommissionRate ?? 0m, grossAmount);

        if (teacher == null)
            return (0m, grossAmount, allocationMode ?? TeacherAllocationMode.CommissionRate, allocationValue ?? 0m, grossAmount);

        if (allocationMode.HasValue && allocationValue.HasValue)
        {
            var selectedShare = allocationMode.Value == TeacherAllocationMode.FixedAmount
                ? allocationValue.Value
                : grossAmount * allocationValue.Value / 100m;

            selectedShare = Math.Clamp(selectedShare, 0m, grossAmount);

            if (revenueOwner == SalesOwnerType.Platform)
                return (grossAmount - selectedShare, selectedShare, allocationMode.Value, allocationValue.Value, grossAmount);

            return (selectedShare, grossAmount - selectedShare, allocationMode.Value, allocationValue.Value, grossAmount);
        }

        // CommissionRate is stored as a percentage (for example, 20 means 20%),
        // not as a fractional multiplier. Keeping the calculation here also makes
        // access-code sales use the same accounting rule as direct purchases.
        var commissionRate = Math.Clamp(teacher.CommissionRate, 0m, 100m);
        var defaultTeacherShare = Math.Round(
            grossAmount * commissionRate / 100m,
            2,
            MidpointRounding.AwayFromZero);

        return (defaultTeacherShare, grossAmount - defaultTeacherShare, TeacherAllocationMode.CommissionRate, commissionRate, grossAmount);
    }

    private static string GenerateSecureCode(int length)
    {
        const string chars = "0123456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }

    private static string HashCode(string plaintext)
    {
        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(hashBytes);
    }
}
