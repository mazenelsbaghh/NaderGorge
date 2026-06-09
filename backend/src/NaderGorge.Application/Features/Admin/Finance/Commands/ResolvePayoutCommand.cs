using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Commands;

public record ResolvePayoutCommand(
    Guid PayoutId,
    PayoutStatus Status,
    string? RejectionReason,
    Guid AdminUserId
) : IRequest<ApiResponse<bool>>;

public class ResolvePayoutCommandHandler : IRequestHandler<ResolvePayoutCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public ResolvePayoutCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<bool>> Handle(ResolvePayoutCommand request, CancellationToken ct)
    {
        if (request.Status == PayoutStatus.Pending)
        {
            return ApiResponse<bool>.Fail("لا يمكن تعديل حالة الطلب إلى معلق");
        }

        if (request.Status == PayoutStatus.Rejected && string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return ApiResponse<bool>.Fail("سبب الرفض مطلوب عند رفض طلب الدفعة");
        }

        try
        {
            await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var payout = await _db.TeacherPayouts
                .FirstOrDefaultAsync(tp => tp.Id == request.PayoutId, ct);

            if (payout == null)
            {
                return ApiResponse<bool>.Fail("طلب الدفعة غير موجود");
            }

            if (payout.Status != PayoutStatus.Pending)
            {
                return ApiResponse<bool>.Fail("تم البت في هذا الطلب مسبقاً");
            }

            var account = await _db.TeacherAccounts
                .FirstOrDefaultAsync(ta => ta.TeacherId == payout.TeacherId, ct);

            if (account == null)
            {
                return ApiResponse<bool>.Fail("حساب المعلم المالي غير موجود");
            }

            var oldStatus = payout.Status;

            if (request.Status == PayoutStatus.Paid)
            {
                if (payout.Amount > account.CurrentBalance)
                {
                    return ApiResponse<bool>.Fail($"رصيد المعلم الحالي ({account.CurrentBalance} ج.م) لا يكفي لصرف الدفعة بقيمة ({payout.Amount} ج.م)");
                }

                // Deduct balance
                account.CurrentBalance -= payout.Amount;
                account.UpdatedAt = DateTime.UtcNow;
            }

            payout.Status = request.Status;
            payout.RejectionReason = request.Status == PayoutStatus.Rejected ? request.RejectionReason : null;
            payout.HandledByUserId = request.AdminUserId;
            payout.HandledAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Audit log
            var auditEntry = new AuditLog
            {
                Action = "ResolvePayout",
                EntityType = nameof(TeacherPayout),
                EntityId = payout.Id,
                PerformedByUserId = request.AdminUserId,
                OldValues = $"Status: {oldStatus}",
                NewValues = $"Status: {request.Status}, RejectionReason: {payout.RejectionReason}",
                CreatedAt = DateTime.UtcNow
            };
            await _audit.AddAsync(auditEntry);

            return ApiResponse<bool>.Ok(true, request.Status == PayoutStatus.Paid ? "تم صرف الدفعة بنجاح" : "تم رفض طلب الدفعة");
        }
        catch (Exception ex) when (IsConcurrencyFailure(ex))
        {
            return ApiResponse<bool>.Fail("تم إجراء عملية متزامنة على هذا الطلب. يرجى المحاولة مرة أخرى.");
        }
    }

    private static bool IsConcurrencyFailure(Exception ex)
    {
        return ex.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("concurrent update", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException != null && IsConcurrencyFailure(ex.InnerException));
    }
}
