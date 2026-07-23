using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public record TeacherFinancialAllocationInput(
    Guid TeacherId,
    TeacherAllocationMode AllocationMode,
    decimal AllocationValue,
    decimal GrossBasisAmount,
    decimal TeacherShareAmount,
    decimal PlatformShareAmount,
    string? StudentNameSnapshot,
    string? StudentPhoneSnapshot,
    string ContentNameSnapshot,
    long? CodeSerialNumber = null,
    TeacherFinancialReviewStatus ReviewStatus = TeacherFinancialReviewStatus.AutoApproved
);

public record TeacherFinancialEventInput(
    TeacherFinancialSourceType SourceType,
    Guid SourceId,
    Guid? StudentId,
    SalesTargetType TargetType,
    Guid TargetId,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal PaidAmount,
    decimal PromotionalAmount,
    decimal PlatformShareAmount,
    string IdempotencyKey,
    string DetailsJson,
    DateTime? OccurredAt,
    TeacherFinancialReviewStatus ReviewStatus,
    IReadOnlyCollection<TeacherFinancialAllocationInput> Allocations
);

public class TeacherAccountingService
{
    private readonly IAppDbContext _db;

    public TeacherAccountingService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TeacherFinancialEvent> RecordEventAsync(TeacherFinancialEventInput input, CancellationToken ct)
    {
        var existing = await _db.TeacherFinancialEvents
            .Include(e => e.Allocations)
            .FirstOrDefaultAsync(e => e.IdempotencyKey == input.IdempotencyKey, ct);

        if (existing != null)
        {
            return existing;
        }

        var evt = new TeacherFinancialEvent
        {
            Id = Guid.NewGuid(),
            SourceType = input.SourceType,
            SourceId = input.SourceId,
            StudentId = input.StudentId,
            TargetType = input.TargetType,
            TargetId = input.TargetId,
            GrossAmount = input.GrossAmount,
            DiscountAmount = input.DiscountAmount,
            PaidAmount = input.PaidAmount,
            PromotionalAmount = input.PromotionalAmount,
            PlatformShareAmount = input.PlatformShareAmount,
            IdempotencyKey = input.IdempotencyKey,
            DetailsJson = string.IsNullOrWhiteSpace(input.DetailsJson) ? "{}" : input.DetailsJson,
            OccurredAt = input.OccurredAt ?? DateTime.UtcNow,
            ReviewStatus = input.ReviewStatus,
            PayoutStatus = input.ReviewStatus == TeacherFinancialReviewStatus.Rejected
                ? TeacherFinancialPayoutStatus.NotEligible
                : TeacherFinancialPayoutStatus.Unpaid
        };

        foreach (var allocationInput in input.Allocations)
        {
            var payoutStatus = allocationInput.ReviewStatus == TeacherFinancialReviewStatus.Rejected
                || allocationInput.TeacherShareAmount == 0m
                    ? TeacherFinancialPayoutStatus.NotEligible
                    : TeacherFinancialPayoutStatus.Unpaid;

            evt.Allocations.Add(new TeacherFinancialAllocation
            {
                Id = Guid.NewGuid(),
                TeacherId = allocationInput.TeacherId,
                AllocationMode = allocationInput.AllocationMode,
                AllocationValue = allocationInput.AllocationValue,
                GrossBasisAmount = allocationInput.GrossBasisAmount,
                TeacherShareAmount = allocationInput.TeacherShareAmount,
                PlatformShareAmount = allocationInput.PlatformShareAmount,
                StudentNameSnapshot = allocationInput.StudentNameSnapshot,
                StudentPhoneSnapshot = allocationInput.StudentPhoneSnapshot,
                ContentNameSnapshot = allocationInput.ContentNameSnapshot,
                CodeSerialNumber = allocationInput.CodeSerialNumber,
                ReviewStatus = allocationInput.ReviewStatus,
                PayoutStatus = payoutStatus
            });
        }

        _db.TeacherFinancialEvents.Add(evt);
        await ApplyApprovedAllocationsToAccounts(evt.Allocations, ct);
        await _db.SaveChangesAsync(ct);
        return evt;
    }

    public async Task<int> ReverseTargetAsync(
        Guid studentId,
        SalesTargetType targetType,
        Guid targetId,
        Guid sourceId,
        string reason,
        CancellationToken ct)
    {
        var idempotencyKey = $"teacher-reversal:{sourceId}:{studentId}:{targetType}:{targetId}";
        var existing = await _db.TeacherFinancialEvents
            .AnyAsync(e => e.IdempotencyKey == idempotencyKey, ct);
        if (existing)
        {
            return 0;
        }

        var allocations = await _db.TeacherFinancialAllocations
            .Include(a => a.TeacherFinancialEvent)
            .Where(a => a.TeacherFinancialEvent.StudentId == studentId
                && a.TeacherFinancialEvent.TargetType == targetType
                && a.TeacherFinancialEvent.TargetId == targetId
                && a.TeacherShareAmount > 0m
                && a.PayoutStatus != TeacherFinancialPayoutStatus.Reversed
                && a.PayoutStatus != TeacherFinancialPayoutStatus.Debt
                && a.ReviewStatus != TeacherFinancialReviewStatus.Rejected
                && a.TeacherFinancialEvent.SourceType != TeacherFinancialSourceType.Refund
                && a.TeacherFinancialEvent.SourceType != TeacherFinancialSourceType.Cancellation)
            .ToListAsync(ct);

        if (allocations.Count == 0)
        {
            return 0;
        }

        var reversalEvent = new TeacherFinancialEvent
        {
            Id = Guid.NewGuid(),
            SourceType = TeacherFinancialSourceType.Cancellation,
            SourceId = sourceId,
            StudentId = studentId,
            TargetType = targetType,
            TargetId = targetId,
            GrossAmount = -allocations.Sum(a => a.GrossBasisAmount),
            DiscountAmount = 0m,
            PaidAmount = 0m,
            PromotionalAmount = 0m,
            PlatformShareAmount = -allocations.Sum(a => a.PlatformShareAmount),
            IdempotencyKey = idempotencyKey,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { reason }),
            OccurredAt = DateTime.UtcNow,
            ReviewStatus = TeacherFinancialReviewStatus.Reversed,
            PayoutStatus = TeacherFinancialPayoutStatus.Reversed
        };

        foreach (var allocation in allocations)
        {
            reversalEvent.Allocations.Add(new TeacherFinancialAllocation
            {
                Id = Guid.NewGuid(),
                TeacherId = allocation.TeacherId,
                AllocationMode = TeacherAllocationMode.Reversal,
                AllocationValue = allocation.TeacherShareAmount,
                GrossBasisAmount = -allocation.GrossBasisAmount,
                TeacherShareAmount = -allocation.TeacherShareAmount,
                PlatformShareAmount = -allocation.PlatformShareAmount,
                StudentNameSnapshot = allocation.StudentNameSnapshot,
                StudentPhoneSnapshot = allocation.StudentPhoneSnapshot,
                ContentNameSnapshot = allocation.ContentNameSnapshot,
                CodeSerialNumber = allocation.CodeSerialNumber,
                ReviewStatus = TeacherFinancialReviewStatus.Reversed,
                PayoutStatus = allocation.PayoutStatus == TeacherFinancialPayoutStatus.Paid
                    ? TeacherFinancialPayoutStatus.Debt
                    : TeacherFinancialPayoutStatus.Reversed
            });

            if (allocation.PayoutStatus == TeacherFinancialPayoutStatus.Paid)
            {
                _db.TeacherPayoutAdjustments.Add(new TeacherPayoutAdjustment
                {
                    Id = Guid.NewGuid(),
                    TeacherId = allocation.TeacherId,
                    RelatedFinancialEventId = allocation.TeacherFinancialEventId,
                    RelatedPayoutId = allocation.PayoutId,
                    Amount = -allocation.TeacherShareAmount,
                    Reason = reason,
                    Status = TeacherPayoutAdjustmentStatus.Open
                });
            }
            else
            {
                var account = await _db.TeacherAccounts.FirstOrDefaultAsync(a => a.TeacherId == allocation.TeacherId, ct);
                if (account != null)
                {
                    account.TotalEarnings = Math.Max(0m, account.TotalEarnings - allocation.TeacherShareAmount);
                    account.CurrentBalance = Math.Max(0m, account.CurrentBalance - allocation.TeacherShareAmount);
                    if (allocation.PayoutStatus == TeacherFinancialPayoutStatus.Reserved)
                    {
                        account.ReservedBalance = Math.Max(0m, account.ReservedBalance - allocation.TeacherShareAmount);
                    }
                    account.UpdatedAt = DateTime.UtcNow;
                }
            }

            allocation.ReviewStatus = TeacherFinancialReviewStatus.Reversed;
            allocation.PayoutStatus = allocation.PayoutStatus == TeacherFinancialPayoutStatus.Paid
                ? TeacherFinancialPayoutStatus.Debt
                : TeacherFinancialPayoutStatus.Reversed;
            allocation.UpdatedAt = DateTime.UtcNow;
        }

        _db.TeacherFinancialEvents.Add(reversalEvent);
        await _db.SaveChangesAsync(ct);
        return allocations.Count;
    }

    private async Task ApplyApprovedAllocationsToAccounts(IEnumerable<TeacherFinancialAllocation> allocations, CancellationToken ct)
    {
        foreach (var allocation in allocations.Where(a =>
                     a.ReviewStatus is TeacherFinancialReviewStatus.AutoApproved or TeacherFinancialReviewStatus.Approved
                     && a.TeacherShareAmount > 0m))
        {
            var account = await _db.TeacherAccounts
                .FirstOrDefaultAsync(a => a.TeacherId == allocation.TeacherId, ct);

            if (account == null)
            {
                var teacher = await _db.TeacherProfiles
                    .FirstOrDefaultAsync(t => t.Id == allocation.TeacherId, ct);

                account = new TeacherAccount
                {
                    Id = Guid.NewGuid(),
                    TeacherId = allocation.TeacherId,
                    TotalEarnings = 0m,
                    CurrentBalance = 0m,
                    ReservedBalance = 0m,
                    CommissionRate = teacher?.CommissionRate ?? 0m
                };
                _db.TeacherAccounts.Add(account);
            }

            account.TotalEarnings += allocation.TeacherShareAmount;
            account.CurrentBalance += allocation.TeacherShareAmount;
            account.UpdatedAt = DateTime.UtcNow;
        }
    }
}
