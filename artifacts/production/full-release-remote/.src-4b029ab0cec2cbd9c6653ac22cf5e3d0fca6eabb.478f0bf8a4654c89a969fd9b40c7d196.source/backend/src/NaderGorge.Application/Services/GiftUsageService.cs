using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class GiftUsageService : IGiftUsageService
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public GiftUsageService(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<bool> TryConsumeAsync(
        Guid studentId,
        GiftTargetType targetType,
        Guid targetId,
        CancellationToken ct = default)
    {
        if (targetType is not (GiftTargetType.Video or GiftTargetType.Exam))
            return false;

        if (_academicScope != null)
        {
            var ownerType = targetType == GiftTargetType.Video
                ? StudentFacingScopeOwnerType.LessonVideo
                : StudentFacingScopeOwnerType.Exam;
            var academicResult = await _academicScope.ValidateStudentCanUseTargetAsync(ownerType, targetId, studentId, ct);
            if (!academicResult.IsEligible)
                return false;
        }

        var now = DateTime.UtcNow;
        if (targetType == GiftTargetType.Exam)
        {
            var hasNonGiftDirectAccess = await _db.StudentAccessGrants.AnyAsync(g =>
                g.UserId == studentId &&
                g.GiftRecipientId == null &&
                g.IsActive &&
                g.GrantType == CodeType.Exam &&
                g.ExamId == targetId &&
                (g.ExpiresAt == null || g.ExpiresAt > now), ct);
            if (hasNonGiftDirectAccess)
                return false;
        }

        var query = _db.StudentAccessGrants
            .Include(g => g.GiftRecipient)
            .ThenInclude(r => r!.GiftIssuance)
            .Where(g => g.UserId == studentId &&
                        g.GiftRecipientId != null &&
                        g.IsActive &&
                        (g.ExpiresAt == null || g.ExpiresAt > now) &&
                        (g.MaxUses == null || g.UsesConsumed < g.MaxUses));

        query = targetType == GiftTargetType.Video
            ? query.Where(g => g.GrantType == CodeType.Video && g.LessonVideoId == targetId)
            : query.Where(g => g.GrantType == CodeType.Exam && g.ExamId == targetId);

        var grant = await query
            .OrderBy(g => g.ExpiresAt == null)
            .ThenBy(g => g.ExpiresAt)
            .ThenBy(g => g.GrantedAt)
            .FirstOrDefaultAsync(ct);

        if (grant?.GiftRecipient == null)
            return false;

        grant.UsesConsumed++;
        grant.GiftRecipient.UsesConsumed++;
        grant.UpdatedAt = now;
        grant.GiftRecipient.UpdatedAt = now;

        if (grant.MaxUses.HasValue && grant.UsesConsumed >= grant.MaxUses.Value)
        {
            grant.IsActive = false;
            grant.GiftRecipient.Status = GiftRecipientStatus.Completed;
        }
        else
        {
            grant.GiftRecipient.Status = GiftRecipientStatus.PartiallyUsed;
        }

        _db.AuditLogs.Add(new Domain.Entities.AuditLog
        {
            Action = "GiftConsumed",
            EntityType = nameof(Domain.Entities.GiftRecipient),
            EntityId = grant.GiftRecipientId,
            PerformedByUserId = studentId,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                targetType,
                targetId,
                usesConsumed = grant.UsesConsumed,
                maxUses = grant.MaxUses
            })
        });

        await _db.SaveChangesAsync(ct);
        if (grant.GiftRecipient.Status == GiftRecipientStatus.Completed)
        {
            var hasRemaining = await _db.GiftRecipients.AnyAsync(x =>
                x.GiftIssuanceId == grant.GiftRecipient.GiftIssuanceId &&
                (x.Status == GiftRecipientStatus.Active || x.Status == GiftRecipientStatus.Granted || x.Status == GiftRecipientStatus.PartiallyUsed), ct);
            if (!hasRemaining)
            {
                var issuance = await _db.GiftIssuances.FirstAsync(x => x.Id == grant.GiftRecipient.GiftIssuanceId, ct);
                issuance.Status = GiftIssuanceStatus.Completed;
                issuance.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
            }
        }
        return true;
    }
}
