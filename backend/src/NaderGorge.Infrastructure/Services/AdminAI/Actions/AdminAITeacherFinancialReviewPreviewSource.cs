using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAITeacherFinancialReviewPreviewSource(IAppDbContext db) : IAdminAIActionPreviewSource
{
    public async Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class
    {
        if (capabilityKey != "admin.finance.teacher-event.review"
            || input is not AdminAIReviewTeacherFinancialAllocationInput review)
            throw new NotSupportedException("Admin AI financial review preview is unavailable.");
        if (actorId == Guid.Empty || review.AllocationId == Guid.Empty
            || review.Status is not (TeacherFinancialReviewStatus.Approved or TeacherFinancialReviewStatus.Rejected)
            || review.Note?.Trim().Length > 1000)
            throw new ArgumentException("Financial review input is invalid.", nameof(input));

        var allocation = await db.TeacherFinancialAllocations.AsNoTracking()
            .Where(item => item.Id == review.AllocationId)
            .Select(item => new
            {
                item.Id, item.TeacherId, item.TeacherFinancialEventId, item.TeacherShareAmount,
                item.PlatformShareAmount, item.ReviewStatus, item.PayoutStatus, item.UpdatedAt,
                item.ContentNameSnapshot, item.TeacherFinancialEvent.SourceType,
                EventReviewStatus = item.TeacherFinancialEvent.ReviewStatus,
                EventUpdatedAt = item.TeacherFinancialEvent.UpdatedAt
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The financial allocation is unavailable.");
        if (allocation.ReviewStatus != TeacherFinancialReviewStatus.PendingReview)
            throw new AdminAIActionPreviewUnavailableException("The financial allocation is no longer pending review.");

        var account = await db.TeacherAccounts.AsNoTracking()
            .Where(item => item.TeacherId == allocation.TeacherId)
            .Select(item => new { item.Version, item.CurrentBalance, item.TotalEarnings })
            .SingleOrDefaultAsync(ct);
        var state = new { allocation, account };
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { capabilityKey, state })));
        return new AdminAIActionPreview(
            "teacher-financial-allocation", $"teacher-financial-allocation:{allocation.Id:D}",
            new
            {
                allocation.ContentNameSnapshot, allocation.TeacherShareAmount,
                allocation.PlatformShareAmount, allocation.ReviewStatus, allocation.PayoutStatus,
                allocation.SourceType, teacherCurrentBalance = account?.CurrentBalance
            },
            new { review.Status, Note = review.Note?.Trim() },
            new
            {
                reviewStatusAfter = review.Status,
                payoutStatusAfter = review.Status == TeacherFinancialReviewStatus.Approved
                    && allocation.TeacherShareAmount > 0m
                    ? TeacherFinancialPayoutStatus.Unpaid : TeacherFinancialPayoutStatus.NotEligible,
                teacherBalanceIncrease = review.Status == TeacherFinancialReviewStatus.Approved
                    ? allocation.TeacherShareAmount : 0m,
                affected = 1
            },
            new { valid = true }, fingerprint);
    }
}
