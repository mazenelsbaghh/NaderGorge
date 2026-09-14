using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CancelPackageGrantCommand(Guid AccessGrantId, bool RefundBalance, Guid AdminId, string? Reason = null) : IRequest<ApiResponse>;

public class CancelPackageGrantCommandHandler : IRequestHandler<CancelPackageGrantCommand, ApiResponse>
{
    private readonly IAppDbContext _context;
    private readonly TeacherAccountingService _teacherAccounting;

    public CancelPackageGrantCommandHandler(IAppDbContext context, TeacherAccountingService teacherAccounting)
    {
        _context = context;
        _teacherAccounting = teacherAccounting;
    }

    public async Task<ApiResponse> Handle(CancelPackageGrantCommand request, CancellationToken cancellationToken)
    {
        var grant = await _context.StudentAccessGrants
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == request.AccessGrantId, cancellationToken);

        if (grant == null) return ApiResponse.Fail("Access grant not found.");
        if (!grant.IsActive) return ApiResponse.Fail("Subscription is already inactive/canceled.");

        grant.IsActive = false;
        grant.CancelledByUserId = request.AdminId;
        grant.CancelledAt = DateTime.UtcNow;
        grant.CancellationReason = request.Reason;

        var grantContext = await ResolveGrantContextAsync(grant, cancellationToken);
        decimal refundedAmount = 0m;
        string contentName = grantContext?.Name ?? "المحتوى";

        if (request.RefundBalance && grantContext?.Price > 0m)
        {
            refundedAmount = grantContext.Price;
            var balance = await _context.StudentBalances
                .FirstOrDefaultAsync(b => b.UserId == grant.UserId, cancellationToken);

            if (balance == null)
            {
                balance = new StudentBalance
                {
                    Id = Guid.NewGuid(),
                    UserId = grant.UserId,
                    CurrentBalance = 0m
                };
                _context.StudentBalances.Add(balance);
            }

            balance.CurrentBalance += refundedAmount;
            balance.UpdatedAt = DateTime.UtcNow;

            var transaction = new BalanceTransaction
            {
                Id = Guid.NewGuid(),
                StudentBalanceId = balance.Id,
                Amount = refundedAmount,
                BalanceAfter = balance.CurrentBalance,
                TransactionType = "Refund",
                ReferenceId = grantContext.TargetId,
                Description = $"إرجاع رصيد {contentName} بعد إلغاء الإدارة",
                CreatedAt = DateTime.UtcNow,
                PerformedByUserId = request.AdminId
            };
            _context.BalanceTransactions.Add(transaction);
        }

        var audit = new AuditLog
        {
            EntityType = "StudentAccessGrant",
            EntityId = grant.Id,
            Action = "CANCEL_PACKAGE_GRANT",
            PerformedByUserId = request.AdminId,
            OldValues = JsonSerializer.Serialize(new { isActive = true }),
            NewValues = JsonSerializer.Serialize(new { isActive = false, refundBalance = request.RefundBalance, refundedAmount })
        };
        _context.AuditLogs.Add(audit);

        if (refundedAmount > 0m)
        {
            var balance = await _context.StudentBalances.FirstOrDefaultAsync(b => b.UserId == grant.UserId, cancellationToken);
            if (balance != null)
            {
                var outboxEvent = new OutboxEvent
                {
                    Type = "BalanceChanged",
                    TargetUserId = grant.UserId.ToString(),
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        newBalance = balance.CurrentBalance,
                        formattedBalance = $"{balance.CurrentBalance:F2} جنيها"
                    })
                };
                _context.OutboxEvents.Add(outboxEvent);
            }
        }

        var accessRevokedEvent = new OutboxEvent
        {
            Type = "PackageAccessRevoked",
            TargetUserId = grant.UserId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new
            {
                packageId = grant.PackageId,
                packageName = contentName,
                userId = grant.UserId
            })
        };
        _context.OutboxEvents.Add(accessRevokedEvent);

        await _context.SaveChangesAsync(cancellationToken);

        if (grantContext != null)
        {
            await _teacherAccounting.ReverseTargetAsync(
                grant.UserId,
                grantContext.TargetType,
                grantContext.TargetId,
                grant.Id,
                request.Reason ?? $"إلغاء اشتراك {contentName}",
                cancellationToken);
        }

        var successMessage = refundedAmount > 0m
            ? $"تم إلغاء اشتراك {contentName} بنجاح وإرجاع {refundedAmount} ج.م إلى رصيد الطالب."
            : $"تم إلغاء اشتراك {contentName} بنجاح دون إرجاع رصيد.";

        return ApiResponse.Ok(successMessage);
    }

    private async Task<GrantCancellationContext?> ResolveGrantContextAsync(StudentAccessGrant grant, CancellationToken ct)
    {
        switch (grant.GrantType)
        {
            case CodeType.Package when grant.PackageId.HasValue:
            {
                var package = await _context.Packages
                    .FirstOrDefaultAsync(p => p.Id == grant.PackageId.Value, ct);
                return package == null
                    ? null
                    : new GrantCancellationContext(package.Name, package.Price, SalesTargetType.Package, package.Id);
            }
            case CodeType.Term when grant.TermId.HasValue:
            {
                var term = await _context.Terms
                    .Include(t => t.Package)
                    .FirstOrDefaultAsync(t => t.Id == grant.TermId.Value, ct);
                return term == null
                    ? null
                    : new GrantCancellationContext($"{term.Package.Name} — {term.Title}", term.Price, SalesTargetType.Term, term.Id);
            }
            case CodeType.Month when grant.ContentSectionId.HasValue:
            {
                var section = await _context.ContentSections
                    .Include(s => s.Term)
                        .ThenInclude(t => t.Package)
                    .FirstOrDefaultAsync(s => s.Id == grant.ContentSectionId.Value, ct);
                return section == null
                    ? null
                    : new GrantCancellationContext($"{section.Term.Package.Name} — {section.Title}", section.Price, SalesTargetType.ContentSection, section.Id);
            }
            case CodeType.Lesson when grant.LessonId.HasValue:
            {
                var lesson = await _context.Lessons
                    .Include(l => l.ContentSection)
                        .ThenInclude(s => s.Term)
                            .ThenInclude(t => t.Package)
                    .FirstOrDefaultAsync(l => l.Id == grant.LessonId.Value, ct);
                return lesson == null
                    ? null
                    : new GrantCancellationContext($"{lesson.ContentSection.Term.Package.Name} — {lesson.Title}", lesson.Price, SalesTargetType.Lesson, lesson.Id);
            }
            case CodeType.Video when grant.LessonVideoId.HasValue:
            {
                var video = await _context.LessonVideos
                    .FirstOrDefaultAsync(v => v.Id == grant.LessonVideoId.Value, ct);
                return video == null
                    ? null
                    : new GrantCancellationContext(video.Title, 0m, SalesTargetType.SpecificVideo, video.Id);
            }
            case CodeType.Video when grant.VideoTypeId.HasValue:
            {
                return new GrantCancellationContext("مجموعة فيديوهات", 0m, SalesTargetType.VideoType, grant.VideoTypeId.Value);
            }
            case CodeType.Exam when grant.ExamId.HasValue:
            {
                return new GrantCancellationContext("امتحان", 0m, SalesTargetType.PublicExam, grant.ExamId.Value);
            }
            default:
                return null;
        }
    }

    private sealed record GrantCancellationContext(string Name, decimal Price, SalesTargetType TargetType, Guid TargetId);
}
