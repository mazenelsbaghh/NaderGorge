using System.Data;
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
    TeacherFinancialReviewStatus ReviewStatus = TeacherFinancialReviewStatus.AutoApproved,
    Guid? AgreementId = null,
    TeacherAgreementScopeType? AgreementScopeType = null,
    Guid? AgreementScopeId = null,
    TeacherAgreementAllocationMode? AgreementAllocationMode = null,
    TeacherPriceBasis? PriceBasis = null,
    TeacherDiscountBearer DiscountBearer = TeacherDiscountBearer.Platform
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
    IReadOnlyCollection<TeacherFinancialAllocationInput> Allocations,
    decimal PlatformDiscountAmount = 0m,
    decimal TeacherDiscountAmount = 0m
);

public class TeacherAccountingService
{
    private sealed record ApprovedTeacherCredit(Guid TeacherId, decimal Amount);

    private readonly IAppDbContext _db;

    public TeacherAccountingService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TeacherFinancialEvent> RecordEventAsync(TeacherFinancialEventInput input, CancellationToken ct)
    {
        if (_db is not DbContext dbContext
            || dbContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL"
            || dbContext.Database.CurrentTransaction != null)
            return await RecordEventCoreAsync(input, ct);

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var financialEvent = await RecordEventCoreAsync(input, ct);
        await transaction.CommitAsync(ct);
        return financialEvent;
    }

    private async Task<TeacherFinancialEvent> RecordEventCoreAsync(TeacherFinancialEventInput input, CancellationToken ct)
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
            PlatformDiscountAmount = input.PlatformDiscountAmount,
            TeacherDiscountAmount = input.TeacherDiscountAmount,
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
                AgreementId = allocationInput.AgreementId,
                AgreementScopeType = allocationInput.AgreementScopeType,
                AgreementScopeId = allocationInput.AgreementScopeId,
                AgreementAllocationMode = allocationInput.AgreementAllocationMode,
                PriceBasis = allocationInput.PriceBasis,
                DiscountBearer = allocationInput.DiscountBearer,
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
        foreach (var credit in ApprovedCredits(allocations))
        {
            if (await TryCreditExistingAccountAtomically(credit, ct))
                continue;

            await CreditTrackedAccount(credit, ct);
        }
    }

    private static IEnumerable<ApprovedTeacherCredit> ApprovedCredits(IEnumerable<TeacherFinancialAllocation> allocations)
    {
        return allocations
            .Where(a =>
                a.ReviewStatus is TeacherFinancialReviewStatus.AutoApproved or TeacherFinancialReviewStatus.Approved
                && a.TeacherShareAmount > 0m)
            .GroupBy(a => a.TeacherId)
            .Select(group => new ApprovedTeacherCredit(
                group.Key,
                group.Sum(allocation => allocation.TeacherShareAmount)));
    }

    private async Task<bool> TryCreditExistingAccountAtomically(ApprovedTeacherCredit credit, CancellationToken ct)
    {
        if (_db is not DbContext dbContext
            || dbContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            return false;

        var now = DateTime.UtcNow;
        var updatedRows = await _db.TeacherAccounts
            .Where(account => account.TeacherId == credit.TeacherId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(account => account.TotalEarnings, account => account.TotalEarnings + credit.Amount)
                .SetProperty(account => account.CurrentBalance, account => account.CurrentBalance + credit.Amount)
                .SetProperty(account => account.Version, account => account.Version + 1)
                .SetProperty(account => account.UpdatedAt, now), ct);

        return updatedRows == 1;
    }

    private async Task CreditTrackedAccount(ApprovedTeacherCredit credit, CancellationToken ct)
    {
        var account = await _db.TeacherAccounts
            .FirstOrDefaultAsync(candidate => candidate.TeacherId == credit.TeacherId, ct);

        if (account == null)
        {
            var teacher = await _db.TeacherProfiles
                .FirstOrDefaultAsync(candidate => candidate.Id == credit.TeacherId, ct);

            account = new TeacherAccount
            {
                Id = Guid.NewGuid(),
                TeacherId = credit.TeacherId,
                CommissionRate = teacher?.CommissionRate ?? 0m
            };
            _db.TeacherAccounts.Add(account);
        }

        account.TotalEarnings += credit.Amount;
        account.CurrentBalance += credit.Amount;
        account.UpdatedAt = DateTime.UtcNow;
    }
}
