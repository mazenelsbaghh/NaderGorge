using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Codes.Queries;

public record ValidateCodeResponseDto(
    string Code,
    CodeType CodeType,
    Guid? TargetId,
    string TargetName,
    Guid? TeacherId,
    string TeacherName,
    string? TeacherProfileImageUrl
);

public record ValidateCodeQuery(string Code, Guid? UserId = null) : IRequest<ApiResponse<ValidateCodeResponseDto>>;

public class ValidateCodeQueryHandler : IRequestHandler<ValidateCodeQuery, ApiResponse<ValidateCodeResponseDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public ValidateCodeQueryHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<ValidateCodeResponseDto>> Handle(ValidateCodeQuery request, CancellationToken ct)
    {
        var accessCode = await _db.AccessCodes
            .AsNoTracking()
            .Include(c => c.CodeGroup)
                .ThenInclude(cg => cg.Teacher)
                    .ThenInclude(t => t!.User)
            .FirstOrDefaultAsync(c => c.CodePlaintext == request.Code, ct);

        if (accessCode == null)
            return ApiResponse<ValidateCodeResponseDto>.Fail("الكود غير صحيح أو تم استخدامه من قبل");

        if (accessCode.IsConsumed)
            return ApiResponse<ValidateCodeResponseDto>.Fail("الكود تم استخدامه من قبل");

        var now = DateTime.UtcNow;
        if (accessCode.ExpiresAt.HasValue && accessCode.ExpiresAt.Value < now)
            return ApiResponse<ValidateCodeResponseDto>.Fail("انتهت صلاحية هذا الكود");

        if (accessCode.CodeGroup.ExpiresAt.HasValue && accessCode.CodeGroup.ExpiresAt.Value < now)
            return ApiResponse<ValidateCodeResponseDto>.Fail("انتهت صلاحية هذه المجموعة من الأكواد");

        var codeGroup = accessCode.CodeGroup;
        var codeType = codeGroup.CodeType;

        Guid? targetId = null;
        string targetName = "شحن رصيد";
        Guid? teacherId = codeGroup.TeacherId;
        string teacherName = codeGroup.Teacher?.User?.FullName ?? "المنصة";
        string? teacherProfileImageUrl = codeGroup.Teacher?.ProfileImageUrl;

        switch (codeType)
        {
            case CodeType.Package:
                targetId = codeGroup.PackageId;
                var pkg = codeGroup.PackageId.HasValue
                    ? await _db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == codeGroup.PackageId.Value, ct)
                    : null;
                targetName = pkg?.Name ?? (codeGroup.TeacherId.HasValue ? "باكدج عام للمدرس" : "باكدج عام للمنصة");
                break;
            case CodeType.Term:
                targetId = codeGroup.TermId;
                var term = await _db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == codeGroup.TermId, ct);
                targetName = term?.Title ?? "ترم دراسي";
                break;
            case CodeType.Month:
                targetId = codeGroup.ContentSectionId;
                var section = await _db.ContentSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == codeGroup.ContentSectionId, ct);
                targetName = section?.Title ?? "شهر/قسم دراسي";
                break;
            case CodeType.Lesson:
                targetId = codeGroup.LessonId;
                var lesson = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == codeGroup.LessonId, ct);
                targetName = lesson?.Title ?? "حصة دراسية";
                break;
            case CodeType.Exam:
                var examTarget = await ExamCodeAvailability.ResolveAsync(
                    _db,
                    codeGroup.ExamId,
                    codeGroup.PublicExamProductId,
                    now,
                    ct);
                if (examTarget == null)
                    return ApiResponse<ValidateCodeResponseDto>.Fail(
                        ExamCodeAvailability.UnavailableMessage,
                        [ExamCodeAvailability.UnavailableErrorCode]);

                targetId = codeGroup.PublicExamProductId ?? examTarget.ExamId;
                targetName = examTarget.Title;
                break;
            case CodeType.Video:
                targetId = codeGroup.VideoTypeId;
                if (codeGroup.VideoTypeId.HasValue)
                {
                    var videoTypeName = await _db.VideoTypes
                        .AsNoTracking()
                        .Where(v => v.Id == codeGroup.VideoTypeId.Value)
                        .Select(v => v.Name)
                        .FirstOrDefaultAsync(ct);

                    var scopeName = await ResolveVideoScopeNameAsync(codeGroup, ct);
                    targetName = scopeName == null
                        ? $"نوع الفيديو: {videoTypeName ?? "نوع محدد"}"
                        : $"نوع الفيديو: {videoTypeName ?? "نوع محدد"} داخل {scopeName}";
                }
                else
                {
                    var selectedVideoNames = await _db.CodeVideoTargets
                        .AsNoTracking()
                        .Where(t => t.CodeGroupId == codeGroup.Id)
                        .OrderBy(t => t.LessonVideo.Order)
                        .Select(t => t.LessonVideo.Title)
                        .Take(3)
                        .ToListAsync(ct);

                    targetName = selectedVideoNames.Count switch
                    {
                        0 => "فيديوهات محددة",
                        1 => $"فيديو محدد: {selectedVideoNames[0]}",
                        _ => $"فيديوهات محددة: {string.Join("، ", selectedVideoNames)}"
                    };
                }
                break;
            case CodeType.Balance:
                targetName = $"شحن رصيد بقيمة {codeGroup.BalanceAmount} جنيه";
                break;
        }

        if (request.UserId.HasValue && _academicScope != null)
        {
            var academicResult = await ValidateCodeAcademicScopeAsync(codeGroup, request.UserId.Value, ct);
            if (!academicResult.IsEligible)
            {
                return ApiResponse<ValidateCodeResponseDto>.Fail(
                    academicResult.Message ?? "هذا الكود غير متاح لنطاقك الدراسي الحالي.",
                    new List<string> { academicResult.ErrorCode ?? "ACADEMIC_SCOPE_DENIED" });
            }
        }

        var dto = new ValidateCodeResponseDto(
            request.Code,
            codeType,
            targetId,
            targetName,
            teacherId,
            teacherName,
            teacherProfileImageUrl
        );

        return ApiResponse<ValidateCodeResponseDto>.Ok(dto);
    }

    private async Task<string?> ResolveVideoScopeNameAsync(NaderGorge.Domain.Entities.CodeGroup codeGroup, CancellationToken ct)
    {
        if (codeGroup.LessonId.HasValue)
        {
            return await _db.Lessons
                .AsNoTracking()
                .Where(x => x.Id == codeGroup.LessonId.Value)
                .Select(x => $"حصة {x.Title}")
                .FirstOrDefaultAsync(ct);
        }

        if (codeGroup.ContentSectionId.HasValue)
        {
            return await _db.ContentSections
                .AsNoTracking()
                .Where(x => x.Id == codeGroup.ContentSectionId.Value)
                .Select(x => $"شهر {x.Title}")
                .FirstOrDefaultAsync(ct);
        }

        if (codeGroup.TermId.HasValue)
        {
            return await _db.Terms
                .AsNoTracking()
                .Where(x => x.Id == codeGroup.TermId.Value)
                .Select(x => $"ترم {x.Title}")
                .FirstOrDefaultAsync(ct);
        }

        if (codeGroup.PackageId.HasValue)
        {
            return await _db.Packages
                .AsNoTracking()
                .Where(x => x.Id == codeGroup.PackageId.Value)
                .Select(x => $"باقة {x.Name}")
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    private async Task<AcademicScopeCheckResult> ValidateCodeAcademicScopeAsync(NaderGorge.Domain.Entities.CodeGroup codeGroup, Guid userId, CancellationToken ct)
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

    private async Task<List<(StudentFacingScopeOwnerType OwnerType, Guid OwnerId)>> ResolveAcademicTargetsAsync(NaderGorge.Domain.Entities.CodeGroup codeGroup, CancellationToken ct)
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
