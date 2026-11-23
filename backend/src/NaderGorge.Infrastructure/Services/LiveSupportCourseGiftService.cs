using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Gifts.Commands;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Features.Admin.Gifts.Queries;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

internal sealed class LiveSupportCourseGiftService(IAppDbContext db, IMediator mediator)
{
    public async Task<IReadOnlyList<GiftLookupDto>> CoursesAsync(Guid studentId, CancellationToken ct)
    {
        var response = await mediator.Send(new GetGiftTargetsLookupQuery(GiftTargetType.Package), ct);
        if (!response.Success || response.Data is null)
            throw new LiveSupportException("COURSE_LOOKUP_FAILED", response.Message ?? "تعذر تحميل كورسات الهدايا.");
        var eligible = await new AcademicScopeService(db).GetEligiblePackageIdsForStudentAsync(response.Data.Select(course => course.Id).ToArray(), studentId, ct);
        var now = DateTime.UtcNow;
        var owned = await db.StudentAccessGrants.AsNoTracking().Where(grant => grant.UserId == studentId
            && grant.IsActive && grant.GrantType == CodeType.Package && grant.PackageId.HasValue
            && (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now))
            .Select(grant => grant.PackageId!.Value).ToListAsync(ct);
        return response.Data.Where(course => eligible.Contains(course.Id) && !owned.Contains(course.Id)).ToArray();
    }

    public async Task<string> IssueAsync(CourseGiftIntent intent, CancellationToken ct)
    {
        if (!(await CoursesAsync(intent.StudentId, ct)).Any(course => course.Id == intent.PackageId))
            throw new LiveSupportException("COURSE_UNAVAILABLE", "الكورس غير متاح كهدية لهذا الطالب أو مشترك فيه بالفعل.");
        var identity = SHA256.HashData(Encoding.UTF8.GetBytes($"support-gift:{intent.ActorId:N}:{intent.ConversationId:N}:{intent.IdempotencyKey}"));
        var requestId = new Guid(identity.AsSpan(0, 16));
        var response = await mediator.Send(new IssueGiftCommand(new(requestId, GiftTargetType.Package, intent.PackageId,
            null, null, null, null, [intent.StudentId], intent.Reason), intent.ActorId), ct);
        var recipient = response.Data?.Recipients.SingleOrDefault();
        if (!response.Success || recipient is null || recipient.Status == GiftRecipientStatus.Failed)
            throw new LiveSupportException("ACTION_VALIDATION_FAILED", recipient?.OutcomeMessage ?? response.Message ?? "تعذر إضافة الهدية.");
        return recipient.Status == GiftRecipientStatus.AlreadyEntitled ? "الطالب لديه اشتراك في الكورس بالفعل." : "تمت إضافة الكورس هدية للطالب دون خصم رصيد.";
    }
}

internal sealed record CourseGiftIntent(Guid StudentId, Guid ActorId, Guid ConversationId, string IdempotencyKey, Guid PackageId, string Reason);
