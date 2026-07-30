using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class PromotionalBalanceService : IPromotionalBalanceService
{
    private readonly IAppDbContext _db;

    public PromotionalBalanceService(IAppDbContext db) => _db = db;

    public async Task ExpireAvailableAsync(Guid studentId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.PromotionalBalanceAllocations
            .Include(x => x.GiftRecipient)
            .ThenInclude(x => x.GiftIssuance)
            .Where(x => x.StudentId == studentId &&
                        x.AvailableAmount > 0 &&
                        x.ExpiresAt != null &&
                        x.ExpiresAt <= now &&
                        (x.Status == PromotionalBalanceStatus.Active || x.Status == PromotionalBalanceStatus.PartiallyUsed))
            .ToListAsync(ct);

        foreach (var allocation in expired)
        {
            allocation.ExpiredAmount += allocation.AvailableAmount;
            allocation.AvailableAmount = 0;
            allocation.Status = PromotionalBalanceStatus.Expired;
            allocation.UpdatedAt = now;
            allocation.GiftRecipient.Status = GiftRecipientStatus.Expired;
            allocation.GiftRecipient.UpdatedAt = now;
            allocation.GiftRecipient.GiftIssuance.Status = GiftIssuanceStatus.Expired;
            allocation.GiftRecipient.GiftIssuance.UpdatedAt = now;
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "GiftExpired",
                EntityType = nameof(PromotionalBalanceAllocation),
                EntityId = allocation.Id,
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { allocation.ExpiredAmount })
            });
        }

        if (expired.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    public Task<Guid?> ResolveTeacherIdAsync(CodeType contentType, Guid contentId, CancellationToken ct = default)
    {
        return contentType switch
        {
            CodeType.Package => _db.Packages.Where(x => x.Id == contentId).Select(x => (Guid?)x.TeacherId).FirstOrDefaultAsync(ct),
            CodeType.Term => _db.Terms.Where(x => x.Id == contentId).Select(x => (Guid?)x.Package.TeacherId).FirstOrDefaultAsync(ct),
            CodeType.Month => _db.ContentSections.Where(x => x.Id == contentId).Select(x => (Guid?)x.Term.Package.TeacherId).FirstOrDefaultAsync(ct),
            CodeType.Lesson => _db.Lessons.Where(x => x.Id == contentId).Select(x => (Guid?)x.ContentSection.Term.Package.TeacherId).FirstOrDefaultAsync(ct),
            CodeType.Exam => _db.PublicExamProducts.Where(x => x.Id == contentId || x.ExamId == contentId).Select(x => x.TeacherId).FirstOrDefaultAsync(ct),
            _ => Task.FromResult<Guid?>(null)
        };
    }

    public async Task<decimal> GetEligibleAmountAsync(Guid studentId, Guid? teacherId, CancellationToken ct = default)
    {
        await ExpireAvailableAsync(studentId, ct);
        return await _db.PromotionalBalanceAllocations
            .Where(x => x.StudentId == studentId &&
                        x.AvailableAmount > 0 &&
                        (x.Status == PromotionalBalanceStatus.Active || x.Status == PromotionalBalanceStatus.PartiallyUsed) &&
                        (x.MaxPurchaseCount == null || x.PurchaseCount < x.MaxPurchaseCount) &&
                        (x.TeacherId == null || x.TeacherId == teacherId))
            .SumAsync(x => x.AvailableAmount, ct);
    }

    public async Task<PromotionalFundingResult> ConsumeAsync(
        Guid studentId,
        Guid? teacherId,
        CodeType contentType,
        Guid contentId,
        decimal price,
        CancellationToken ct = default)
    {
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        await ExpireAvailableAsync(studentId, ct);
        var operationId = Guid.NewGuid();
        var remaining = price;
        var allocationIds = new List<Guid>();
        var allocations = await _db.PromotionalBalanceAllocations
            .Include(x => x.GiftRecipient)
            .ThenInclude(x => x.GiftIssuance)
            .Where(x => x.StudentId == studentId &&
                        x.AvailableAmount > 0 &&
                        (x.Status == PromotionalBalanceStatus.Active || x.Status == PromotionalBalanceStatus.PartiallyUsed) &&
                        (x.MaxPurchaseCount == null || x.PurchaseCount < x.MaxPurchaseCount) &&
                        (x.TeacherId == null || x.TeacherId == teacherId))
            .OrderBy(x => x.ExpiresAt == null)
            .ThenBy(x => x.ExpiresAt)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        foreach (var allocation in allocations)
        {
            if (remaining <= 0)
                break;

            var amount = Math.Min(allocation.AvailableAmount, remaining);
            allocation.AvailableAmount -= amount;
            allocation.ConsumedAmount += amount;
            allocation.PurchaseCount++;
            allocation.Status = allocation.AvailableAmount == 0
                ? PromotionalBalanceStatus.Consumed
                : PromotionalBalanceStatus.PartiallyUsed;
            allocation.UpdatedAt = DateTime.UtcNow;
            allocation.GiftRecipient.UsesConsumed = allocation.PurchaseCount;
            allocation.GiftRecipient.Status = allocation.AvailableAmount == 0
                ? GiftRecipientStatus.Completed
                : GiftRecipientStatus.PartiallyUsed;
            allocation.GiftRecipient.UpdatedAt = DateTime.UtcNow;

            if (allocation.MaxPurchaseCount.HasValue && allocation.PurchaseCount >= allocation.MaxPurchaseCount.Value && allocation.AvailableAmount > 0)
            {
                allocation.ExpiredAmount += allocation.AvailableAmount;
                allocation.AvailableAmount = 0;
                allocation.Status = PromotionalBalanceStatus.Consumed;
                allocation.GiftRecipient.Status = GiftRecipientStatus.Completed;
            }

            _db.PromotionalBalanceUsages.Add(new PromotionalBalanceUsage
            {
                AllocationId = allocation.Id,
                GiftRecipientId = allocation.GiftRecipientId,
                PurchaseOperationId = operationId,
                ContentType = contentType,
                ContentId = contentId,
                Amount = amount
            });
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "GiftConsumed",
                EntityType = nameof(PromotionalBalanceAllocation),
                EntityId = allocation.Id,
                PerformedByUserId = studentId,
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    operationId,
                    contentType,
                    contentId,
                    amount,
                    allocation.AvailableAmount
                })
            });

            allocationIds.Add(allocation.Id);
            remaining -= amount;
        }

        await _db.SaveChangesAsync(ct);
        await CompleteFinishedIssuancesAsync(allocations.Select(x => x.GiftRecipient.GiftIssuanceId).Distinct(), ct);
        return new PromotionalFundingResult(operationId, price - remaining, remaining, allocationIds);
    }

    private async Task CompleteFinishedIssuancesAsync(IEnumerable<Guid> issuanceIds, CancellationToken ct)
    {
        foreach (var issuanceId in issuanceIds)
        {
            var hasRemaining = await _db.GiftRecipients.AnyAsync(x =>
                x.GiftIssuanceId == issuanceId &&
                (x.Status == GiftRecipientStatus.Active || x.Status == GiftRecipientStatus.Granted || x.Status == GiftRecipientStatus.PartiallyUsed), ct);
            if (hasRemaining)
                continue;

            var issuance = await _db.GiftIssuances.FirstAsync(x => x.Id == issuanceId, ct);
            if (issuance.Status is not (GiftIssuanceStatus.Revoked or GiftIssuanceStatus.Expired))
                issuance.Status = GiftIssuanceStatus.Completed;
        }
        await _db.SaveChangesAsync(ct);
    }
}
