using System.Data;
using System.Data.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Commands;

public sealed record ReviewTeacherFinancialAllocationCommand(
    Guid AllocationId,
    TeacherFinancialReviewStatus Status,
    Guid ActorUserId,
    string? Note = null,
    string? OperationId = null) : IRequest<ApiResponse<bool>>;

public sealed class ReviewTeacherFinancialAllocationCommandHandler(IAppDbContext db)
    : IRequestHandler<ReviewTeacherFinancialAllocationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ReviewTeacherFinancialAllocationCommand request, CancellationToken ct)
    {
        if (request.Status is not (TeacherFinancialReviewStatus.Approved or TeacherFinancialReviewStatus.Rejected))
            return ApiResponse<bool>.Fail("حالة المراجعة يجب أن تكون Approved أو Rejected", ["INVALID_STATUS"]);
        if (request.ActorUserId == Guid.Empty)
            return ApiResponse<bool>.Fail("معرّف المسؤول مطلوب", ["ACTOR_REQUIRED"]);

        var note = request.Note?.Trim();
        if (note?.Length > 1000)
            return ApiResponse<bool>.Fail("ملاحظة المراجعة طويلة جدًا", ["NOTE_TOO_LONG"]);
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<bool>.Fail("معرّف العملية غير صالح", ["INVALID_OPERATION_ID"]);

        try
        {
            return await ReviewInTransactionAsync(request, note, operationId, ct);
        }
        catch (Exception exception) when (IsConcurrentReview(exception))
        {
            return ApiResponse<bool>.Fail("تغيّرت مراجعة البند المالي أثناء التنفيذ؛ حدّث الصفحة وأعد المحاولة", ["CONCURRENT_REVIEW"]);
        }
    }

    private async Task<ApiResponse<bool>> ReviewInTransactionAsync(
        ReviewTeacherFinancialAllocationCommand request, string? note, string? operationId, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (operationId is not null)
        {
            var prior = await db.TeacherFinancialAllocations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ReviewOperationId == operationId, ct);
            if (prior is not null)
            {
                return prior.Id == request.AllocationId && prior.ReviewStatus == request.Status
                    && prior.ReviewActorUserId == request.ActorUserId && prior.ReviewNote == note
                    ? ApiResponse<bool>.Ok(true)
                    : ApiResponse<bool>.Fail("معرّف العملية مستخدم لمراجعة أخرى", ["IDEMPOTENCY_CONFLICT"]);
            }
        }

        var allocation = await db.TeacherFinancialAllocations
            .Include(item => item.TeacherFinancialEvent)
            .SingleOrDefaultAsync(item => item.Id == request.AllocationId, ct);
        if (allocation is null)
            return ApiResponse<bool>.Fail("البند المالي غير موجود", ["NOT_FOUND"]);
        if (allocation.ReviewStatus != TeacherFinancialReviewStatus.PendingReview)
            return ApiResponse<bool>.Fail("يمكن مراجعة البنود المعلقة فقط", ["ALREADY_REVIEWED"]);

        allocation.ReviewStatus = request.Status;
        allocation.UpdatedAt = DateTime.UtcNow;
        allocation.ReviewOperationId = operationId;
        allocation.ReviewActorUserId = request.ActorUserId;
        allocation.ReviewNote = note;
        allocation.PayoutStatus = request.Status == TeacherFinancialReviewStatus.Rejected || allocation.TeacherShareAmount <= 0m
            ? TeacherFinancialPayoutStatus.NotEligible
            : TeacherFinancialPayoutStatus.Unpaid;

        var eventAllocations = await db.TeacherFinancialAllocations
            .Where(item => item.TeacherFinancialEventId == allocation.TeacherFinancialEventId)
            .ToListAsync(ct);
        allocation.TeacherFinancialEvent.ReviewStatus = eventAllocations.Any(item => item.Id != allocation.Id && item.ReviewStatus == TeacherFinancialReviewStatus.PendingReview)
            ? TeacherFinancialReviewStatus.PendingReview
            : eventAllocations.Any(item => item.Id != allocation.Id && item.ReviewStatus == TeacherFinancialReviewStatus.Approved)
              || request.Status == TeacherFinancialReviewStatus.Approved
                ? TeacherFinancialReviewStatus.Approved
                : TeacherFinancialReviewStatus.Rejected;
        allocation.TeacherFinancialEvent.UpdatedAt = DateTime.UtcNow;

        if (request.Status == TeacherFinancialReviewStatus.Approved && allocation.TeacherShareAmount > 0m)
        {
            var account = await db.TeacherAccounts.SingleOrDefaultAsync(item => item.TeacherId == allocation.TeacherId, ct);
            if (account is null)
            {
                var teacher = await db.TeacherProfiles.SingleOrDefaultAsync(item => item.Id == allocation.TeacherId, ct);
                account = new TeacherAccount
                {
                    TeacherId = allocation.TeacherId,
                    CommissionRate = teacher?.CommissionRate ?? 0m
                };
                db.TeacherAccounts.Add(account);
            }
            account.TotalEarnings += allocation.TeacherShareAmount;
            account.CurrentBalance += allocation.TeacherShareAmount;
            account.Version++;
            account.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    private static bool IsConcurrentReview(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException
                || current is DbException { SqlState: "40001" or "23505" })
                return true;
        return false;
    }
}
