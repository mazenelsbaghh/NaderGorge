using System.Text.Json;
using System.Data;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Gifts.Commands;

public sealed record IssueGiftCommand(IssueGiftRequest Request, Guid IssuedByUserId)
    : IRequest<ApiResponse<IssueGiftResultDto>>;

public sealed class IssueGiftCommandValidator : AbstractValidator<IssueGiftCommand>
{
    public IssueGiftCommandValidator()
    {
        RuleFor(x => x.Request.RequestId).NotEmpty();
        RuleFor(x => x.Request.StudentIds).NotEmpty().Must(x => x.Distinct().Count() <= 100)
            .WithMessage("يمكن اختيار 100 طالب كحد أقصى.");
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.ExpiresAt).Must(x => !x.HasValue || x.Value > DateTime.UtcNow)
            .WithMessage("تاريخ الانتهاء يجب أن يكون في المستقبل.");
        RuleFor(x => x.Request.MaxUses).Must(x => !x.HasValue || x.Value > 0)
            .WithMessage("حد الاستخدام يجب أن يكون أكبر من صفر.");
        RuleFor(x => x.Request).Must(HasValidTargetShape)
            .WithMessage("بيانات هدف الهدية غير متوافقة مع نوعها.");
    }

    private static bool HasValidTargetShape(IssueGiftRequest request)
    {
        var balance = request.TargetType is GiftTargetType.GeneralBalance or GiftTargetType.TeacherBalance;
        if (balance)
        {
            if (request.TargetId.HasValue || request.Amount is null or <= 0)
                return false;
            if (request.TargetType == GiftTargetType.TeacherBalance != request.TeacherId.HasValue)
                return false;
        }
        else if (!request.TargetId.HasValue || request.TeacherId.HasValue || request.Amount.HasValue)
        {
            return false;
        }

        return request.MaxUses == null || request.TargetType is GiftTargetType.Video or GiftTargetType.Exam or GiftTargetType.GeneralBalance or GiftTargetType.TeacherBalance;
    }
}

public sealed class IssueGiftCommandHandler
    : IRequestHandler<IssueGiftCommand, ApiResponse<IssueGiftResultDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;
    private readonly BalanceService _balanceService;
    private readonly IAcademicScopeService? _academicScope;

    public IssueGiftCommandHandler(IAppDbContext db, IAccessCheckService access, BalanceService balanceService, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _access = access;
        _balanceService = balanceService;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<IssueGiftResultDto>> Handle(IssueGiftCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var existing = await _db.GiftIssuances
            .AsNoTracking()
            .Include(x => x.Recipients)
            .ThenInclude(x => x.Student)
            .FirstOrDefaultAsync(x => x.RequestId == request.RequestId, ct);

        if (existing != null)
            return ApiResponse<IssueGiftResultDto>.Ok(await MapAsync(existing, true, ct), "تم إرجاع نفس الإصدار بدون تكرار الهدية.");

        var target = await ResolveTargetAsync(request, ct);
        if (target == null)
            return ApiResponse<IssueGiftResultDto>.Fail("هدف الهدية غير موجود أو غير نشط.", ["TARGET_NOT_FOUND"]);

        var targetScopeResult = await ValidateGiftTargetHasScopeAsync(request, ct);
        if (!targetScopeResult.IsEligible)
        {
            return ApiResponse<IssueGiftResultDto>.Fail(
                targetScopeResult.Message ?? "هدف الهدية غير مربوط بنطاق أكاديمي صالح.",
                [targetScopeResult.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED"]);
        }

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var studentIds = request.StudentIds.Distinct().ToList();
        var students = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Where(x => studentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var issuance = new GiftIssuance
        {
            RequestId = request.RequestId,
            TargetType = request.TargetType,
            TeacherId = request.TeacherId,
            Amount = request.Amount,
            ExpiresAt = request.ExpiresAt,
            MaxUses = request.MaxUses,
            Reason = request.Reason.Trim(),
            IssuedByUserId = command.IssuedByUserId
        };
        SetTargetId(issuance, request.TargetId);

        foreach (var studentId in studentIds)
        {
            var recipient = new GiftRecipient
            {
                GiftIssuance = issuance,
                StudentId = studentId,
                Status = GiftRecipientStatus.Failed,
                OutcomeCode = "STUDENT_NOT_FOUND",
                OutcomeMessage = "الطالب غير موجود."
            };

            if (!students.TryGetValue(studentId, out var student) ||
                !student.IsActive ||
                !student.UserRoles.Any(x => x.Role.Type == RoleType.Student))
            {
                issuance.Recipients.Add(recipient);
                continue;
            }

            var studentScopeResult = await ValidateGiftTargetForStudentAsync(request, studentId, ct);
            if (!studentScopeResult.IsEligible)
            {
                recipient.OutcomeCode = "ACADEMIC_SCOPE_DENIED";
                recipient.OutcomeMessage = studentScopeResult.Message ?? "الهدية غير متاحة لنطاق الطالب الدراسي الحالي.";
                issuance.Recipients.Add(recipient);
                _db.AuditLogs.Add(new AuditLog
                {
                    Action = "AcademicScopeDeniedGiftRecipient",
                    EntityType = nameof(GiftIssuance),
                    EntityId = issuance.Id,
                    PerformedByUserId = command.IssuedByUserId,
                    NewValues = JsonSerializer.Serialize(new
                    {
                        request.RequestId,
                        request.TargetType,
                        request.TargetId,
                        studentId,
                        studentScopeResult.ErrorCode
                    })
                });
                continue;
            }

            var alreadyEntitled = !IsBalance(request.TargetType) &&
                await HasEquivalentAccessAsync(studentId, request.TargetType, request.TargetId!.Value, ct);
            if (alreadyEntitled)
            {
                recipient.Status = GiftRecipientStatus.AlreadyEntitled;
                recipient.OutcomeCode = "ALREADY_ENTITLED";
                recipient.OutcomeMessage = "الطالب لديه وصول فعّال مكافئ بالفعل.";
                issuance.Recipients.Add(recipient);
                continue;
            }

            recipient.Status = GiftRecipientStatus.Active;
            recipient.OutcomeCode = "GRANTED";
            recipient.OutcomeMessage = null;

            if (IsBalance(request.TargetType))
            {
                if (request.TargetType == GiftTargetType.GeneralBalance)
                {
                    recipient.Status = GiftRecipientStatus.Completed;
                    recipient.OutcomeCode = "GRANTED_TO_GENERAL_BALANCE";
                    recipient.OutcomeMessage = "تمت إضافة الهدية إلى الرصيد العام للطالب.";

                    await _balanceService.AddCredit(
                        studentId,
                        request.Amount!.Value,
                        $"هدية من المنصة: {request.Reason.Trim()}",
                        recipient.Id,
                        "PlatformGift",
                        ct);
                }
                else
                {
                    recipient.PromotionalBalanceAllocation = new PromotionalBalanceAllocation
                    {
                        StudentId = studentId,
                        TeacherId = request.TeacherId,
                        OriginalAmount = request.Amount!.Value,
                        AvailableAmount = request.Amount.Value,
                        ExpiresAt = request.ExpiresAt,
                        MaxPurchaseCount = request.MaxUses
                    };
                }
            }
            else
            {
                recipient.AccessGrant = BuildAccessGrant(studentId, recipient, issuance);
            }

            issuance.Recipients.Add(recipient);
        }

        var successCount = issuance.Recipients.Count(x => x.Status == GiftRecipientStatus.Active);
        issuance.Status = successCount == 0
            ? GiftIssuanceStatus.Completed
            : successCount == issuance.Recipients.Count
                ? GiftIssuanceStatus.Active
                : GiftIssuanceStatus.PartiallySuccessful;

        _db.GiftIssuances.Add(issuance);
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "GiftIssued",
            EntityType = nameof(GiftIssuance),
            EntityId = issuance.Id,
            PerformedByUserId = command.IssuedByUserId,
            NewValues = JsonSerializer.Serialize(new
            {
                issuance.RequestId,
                issuance.TargetType,
                target = target.Name,
                recipients = issuance.Recipients.Count,
                succeeded = successCount,
                issuance.Reason
            })
        });

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponse<IssueGiftResultDto>.Ok(await MapAsync(issuance, false, ct), "تم إصدار الهدية وتسجيل نتائج المستفيدين.");
    }

    private sealed record TargetDescriptor(string Name, Guid? TeacherId);

    private async Task<TargetDescriptor?> ResolveTargetAsync(IssueGiftRequest request, CancellationToken ct)
    {
        if (request.TargetType == GiftTargetType.GeneralBalance)
            return new TargetDescriptor("رصيد عام من المنصة", null);

        if (request.TargetType == GiftTargetType.TeacherBalance)
        {
            return await _db.TeacherProfiles
                .Where(x => x.Id == request.TeacherId && x.User.IsActive)
                .Select(x => new TargetDescriptor($"رصيد مدرس: {x.User.FullName}", x.Id))
                .FirstOrDefaultAsync(ct);
        }

        var id = request.TargetId!.Value;
        return request.TargetType switch
        {
            GiftTargetType.Package => await _db.Packages.Where(x => x.Id == id && x.IsActive).Select(x => new TargetDescriptor(x.Name, x.TeacherId)).FirstOrDefaultAsync(ct),
            GiftTargetType.Lesson => await _db.Lessons.Where(x => x.Id == id).Select(x => new TargetDescriptor(x.Title, x.ContentSection.Term.Package.TeacherId)).FirstOrDefaultAsync(ct),
            GiftTargetType.Video => await _db.LessonVideos.Where(x => x.Id == id && x.IsActive).Select(x => new TargetDescriptor(x.Title, x.Lesson.ContentSection.Term.Package.TeacherId)).FirstOrDefaultAsync(ct),
            GiftTargetType.Exam => await _db.Exams.Where(x => x.Id == id).Select(x => new TargetDescriptor(x.Title, x.CreatedByTeacherId)).FirstOrDefaultAsync(ct),
            _ => null
        };
    }

    private Task<bool> HasEquivalentAccessAsync(Guid studentId, GiftTargetType type, Guid targetId, CancellationToken ct) => type switch
    {
        GiftTargetType.Package => _access.HasAccessToPackageAsync(studentId, targetId, ct),
        GiftTargetType.Lesson => _access.HasAccessToLessonAsync(studentId, targetId, ct),
        GiftTargetType.Video => _access.HasAccessToVideoAsync(studentId, targetId, ct),
        GiftTargetType.Exam => _access.HasAccessToExamAsync(studentId, targetId, ct),
        _ => Task.FromResult(false)
    };

    private static bool IsBalance(GiftTargetType type) => type is GiftTargetType.GeneralBalance or GiftTargetType.TeacherBalance;

    private async Task<AcademicScopeCheckResult> ValidateGiftTargetHasScopeAsync(IssueGiftRequest request, CancellationToken ct)
    {
        if (_academicScope == null || request.TargetType == GiftTargetType.GeneralBalance)
            return AcademicScopeCheckResult.Eligible();

        var target = ResolveAcademicOwner(request);
        if (target.OwnerType == null)
            return AcademicScopeCheckResult.Eligible();

        return await _academicScope.ValidateTargetHasScopeAsync(target.OwnerType.Value, target.OwnerId, ct);
    }

    private async Task<AcademicScopeCheckResult> ValidateGiftTargetForStudentAsync(IssueGiftRequest request, Guid studentId, CancellationToken ct)
    {
        if (_academicScope == null || request.TargetType == GiftTargetType.GeneralBalance)
            return AcademicScopeCheckResult.Eligible();

        var target = ResolveAcademicOwner(request);
        if (target.OwnerType == null)
            return AcademicScopeCheckResult.Eligible();

        return await _academicScope.ValidateStudentCanUseTargetAsync(target.OwnerType.Value, target.OwnerId, studentId, ct);
    }

    private static (StudentFacingScopeOwnerType? OwnerType, Guid OwnerId) ResolveAcademicOwner(IssueGiftRequest request)
    {
        return request.TargetType switch
        {
            GiftTargetType.Package when request.TargetId.HasValue => (StudentFacingScopeOwnerType.Package, request.TargetId.Value),
            GiftTargetType.Lesson when request.TargetId.HasValue => (StudentFacingScopeOwnerType.Lesson, request.TargetId.Value),
            GiftTargetType.Video when request.TargetId.HasValue => (StudentFacingScopeOwnerType.LessonVideo, request.TargetId.Value),
            GiftTargetType.Exam when request.TargetId.HasValue => (StudentFacingScopeOwnerType.Exam, request.TargetId.Value),
            GiftTargetType.TeacherBalance when request.TeacherId.HasValue => (StudentFacingScopeOwnerType.Teacher, request.TeacherId.Value),
            _ => (null, Guid.Empty)
        };
    }

    private static void SetTargetId(GiftIssuance issuance, Guid? targetId)
    {
        switch (issuance.TargetType)
        {
            case GiftTargetType.Package: issuance.PackageId = targetId; break;
            case GiftTargetType.Lesson: issuance.LessonId = targetId; break;
            case GiftTargetType.Video: issuance.LessonVideoId = targetId; break;
            case GiftTargetType.Exam: issuance.ExamId = targetId; break;
        }
    }

    private static StudentAccessGrant BuildAccessGrant(Guid studentId, GiftRecipient recipient, GiftIssuance issuance)
    {
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GiftRecipient = recipient,
            GrantedAt = DateTime.UtcNow,
            ExpiresAt = issuance.ExpiresAt,
            MaxUses = issuance.MaxUses,
            IsActive = true
        };

        switch (issuance.TargetType)
        {
            case GiftTargetType.Package:
                grant.GrantType = CodeType.Package;
                grant.PackageId = issuance.PackageId;
                break;
            case GiftTargetType.Lesson:
                grant.GrantType = CodeType.Lesson;
                grant.LessonId = issuance.LessonId;
                break;
            case GiftTargetType.Video:
                grant.GrantType = CodeType.Video;
                grant.LessonVideoId = issuance.LessonVideoId;
                break;
            case GiftTargetType.Exam:
                grant.GrantType = CodeType.Exam;
                grant.ExamId = issuance.ExamId;
                break;
        }

        return grant;
    }

    private async Task<IssueGiftResultDto> MapAsync(GiftIssuance issuance, bool replay, CancellationToken ct)
    {
        var target = await ResolveTargetAsync(new IssueGiftRequest(
            issuance.RequestId,
            issuance.TargetType,
            issuance.PackageId ?? issuance.LessonId ?? issuance.LessonVideoId ?? issuance.ExamId,
            issuance.TeacherId,
            issuance.Amount,
            issuance.ExpiresAt,
            issuance.MaxUses,
            Array.Empty<Guid>(),
            issuance.Reason), ct);

        var academicScopes = await ResolveTargetScopeSummariesAsync(
            new IssueGiftRequest(
                issuance.RequestId,
                issuance.TargetType,
                issuance.PackageId ?? issuance.LessonId ?? issuance.LessonVideoId ?? issuance.ExamId,
                issuance.TeacherId,
                issuance.Amount,
                issuance.ExpiresAt,
                issuance.MaxUses,
                Array.Empty<Guid>(),
                issuance.Reason),
            ct);

        return new IssueGiftResultDto(
            issuance.Id,
            issuance.RequestId,
            issuance.TargetType,
            issuance.Status,
            target?.Name ?? "هدف غير متاح",
            issuance.Amount,
            issuance.ExpiresAt,
            issuance.MaxUses,
            issuance.Reason,
            issuance.CreatedAt,
            replay,
            academicScopes,
            issuance.Recipients.Select(x => new GiftRecipientResultDto(
                x.StudentId,
                x.Student?.FullName ?? "طالب غير متاح",
                x.Status,
                x.OutcomeCode,
                x.OutcomeMessage,
                x.UsesConsumed,
                issuance.MaxUses)).ToList());
    }

    private async Task<IReadOnlyList<AcademicScopeSummaryDto>?> ResolveTargetScopeSummariesAsync(IssueGiftRequest request, CancellationToken ct)
    {
        var target = ResolveAcademicOwner(request);
        if (target.OwnerType == null)
            return null;

        var scopes = await _db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == target.OwnerType.Value && x.OwnerId == target.OwnerId)
            .ToListAsync(ct);

        return AcademicScopeService.ToScopeSummaries(scopes);
    }
}
