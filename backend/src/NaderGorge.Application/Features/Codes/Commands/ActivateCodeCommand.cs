using System.Data;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
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
    private readonly BalanceService _balanceService;

    public ActivateCodeCommandHandler(IAppDbContext db, BalanceService balanceService)
    {
        _db = db;
        _balanceService = balanceService;
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

            var consumedRows = await _db.AccessCodes
                .Where(c => c.Id == accessCode.Id && !c.IsConsumed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.IsConsumed, true)
                    .SetProperty(c => c.ConsumedByUserId, user.Id)
                    .SetProperty(c => c.ConsumedAt, now)
                    .SetProperty(c => c.UpdatedAt, now), ct);

            if (consumedRows != 1)
                throw new KeyNotFoundException("Invalid or already used code");

            var codeGroup = accessCode.CodeGroup;
            var codeType = codeGroup.CodeType;
            Guid grantId;
            string? redirectUrl = null;

            if (codeType == CodeType.Balance)
            {
                // Balance code: add credit, no access grant
                var amount = codeGroup.BalanceAmount ?? 0m;
                if (amount <= 0)
                    throw new InvalidOperationException("Invalid balance code amount.");

                var tx = await _balanceService.AddCredit(user.Id, amount, $"Code redemption: {codeGroup.Name}", accessCode.Id, ct);
                grantId = tx.Id; // Use tx ID as reference

                var balanceMaskedCode = request.Code.Length > 4 ? request.Code[..4] + "****" : "****";
                _db.AuditLogs.Add(new AuditLog
                {
                    Action = "ActivateCode",
                    EntityType = "AccessCode",
                    EntityId = accessCode.Id,
                    PerformedByUserId = user.Id,
                    NewValues = $"CodePlaintext: {balanceMaskedCode}, Type: {CodeType.Balance}, Amount: {amount}",
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ApiResponse<ActivateCodeResponse>.Ok(
                    new ActivateCodeResponse(grantId, $"تم شحن رصيدك بـ {amount} جنيه", CodeType.Balance, "/student/balance"));
            }

            // For all other code types: create access grants
            if (codeType == CodeType.Video)
            {
                // Video codes: create grants for each target video
                var videoTargets = codeGroup.CodeVideoTargets;
                if (!videoTargets.Any())
                    throw new InvalidOperationException("Video code has no target videos.");

                Guid? lastGrantId = null;
                foreach (var target in videoTargets)
                {
                    var grant = new StudentAccessGrant
                    {
                        UserId = user.Id,
                        LessonVideoId = target.LessonVideoId,
                        GrantType = CodeType.Video,
                        AccessCodeId = accessCode.Id,
                        IsActive = true,
                        ExpiresAt = codeGroup.ExpiresAt
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
                    ExpiresAt = codeGroup.ExpiresAt
                };

                // Set the appropriate target FK
                switch (codeType)
                {
                    case CodeType.Package:
                        grant.PackageId = codeGroup.PackageId;
                        redirectUrl = $"/student/packages/{codeGroup.PackageId}";
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
                        redirectUrl = $"/student/exams/{codeGroup.ExamId}";
                        break;
                }

                _db.StudentAccessGrants.Add(grant);
                grantId = grant.Id;
            }

            // Calculate and credit teacher commission (except for balance codes which don't directly purchase items)
            if (codeType != CodeType.Balance)
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

                decimal finalPrice = itemPrice;
                if (codeGroup.DiscountPercentage.HasValue && codeGroup.DiscountPercentage.Value > 0)
                {
                    finalPrice = itemPrice * (1m - (codeGroup.DiscountPercentage.Value / 100m));
                }

                var teacherProfile = await _db.TeacherProfiles
                    .FirstOrDefaultAsync(tp => tp.Id == codeGroup.TeacherId, ct);

                if (teacherProfile != null)
                {
                    var commissionRate = teacherProfile.CommissionRate;
                    var commissionEarned = finalPrice * commissionRate;

                    var teacherAccount = await _db.TeacherAccounts
                        .FirstOrDefaultAsync(ta => ta.TeacherId == teacherProfile.Id, ct);

                    if (teacherAccount == null)
                    {
                        teacherAccount = new TeacherAccount
                        {
                            Id = Guid.NewGuid(),
                            TeacherId = teacherProfile.Id,
                            TotalEarnings = 0m,
                            CurrentBalance = 0m,
                            CommissionRate = commissionRate
                        };
                        _db.TeacherAccounts.Add(teacherAccount);
                    }

                    teacherAccount.TotalEarnings += commissionEarned;
                    teacherAccount.CurrentBalance += commissionEarned;
                    teacherAccount.UpdatedAt = DateTime.UtcNow;

                    var activationLog = new AccessCodeActivationLog
                    {
                        Id = Guid.NewGuid(),
                        AccessCodeId = accessCode.Id,
                        StudentId = user.Id,
                        PackageId = codeGroup.PackageId,
                        TeacherId = teacherProfile.Id,
                        Price = finalPrice,
                        CommissionRate = commissionRate,
                        CommissionEarned = commissionEarned,
                        ActivatedAt = DateTime.UtcNow
                    };
                    _db.AccessCodeActivationLogs.Add(activationLog);
                }
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

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var message = codeType switch
            {
                CodeType.Package => "تم تفعيل الباكدج بنجاح!",
                CodeType.Term => "تم تفعيل الترم بنجاح!",
                CodeType.Month => "تم تفعيل الشهر بنجاح!",
                CodeType.Lesson => "تم تفعيل الحصة بنجاح!",
                CodeType.Video => "تم تفعيل الفيديوهات بنجاح!",
                CodeType.Exam => "تم تفعيل الامتحان بنجاح!",
                _ => "تم تفعيل الكود بنجاح!"
            };

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
}
