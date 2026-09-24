using NaderGorge.Application.Interfaces.Finance;
using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Services;

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
    private readonly IFinancialPostingService _posting;

    public ResolvePayoutCommandHandler(IAppDbContext db, IAuditRepository audit, IFinancialPostingService posting)
    {
        _db = db;
        _audit = audit;
        _posting = posting;
    }

    public async Task<ApiResponse<bool>> Handle(ResolvePayoutCommand request, CancellationToken ct)
    {
        return await SerializationRetryHelper.ExecuteAsync(
            async retryCt =>
            {
                try { return await HandleOnce(request, retryCt); }
                catch (Exception ex) when (SerializationRetryHelper.IsSerializationFailure(ex))
                {
                    if (_db is DbContext context) context.ChangeTracker.Clear();
                    throw;
                }
            },
            ct);
    }

    private async Task<ApiResponse<bool>> HandleOnce(ResolvePayoutCommand request, CancellationToken ct)
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

            if (payout.Status == PayoutStatus.Paid || payout.Status == PayoutStatus.Rejected)
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
            if (request.Status is PayoutStatus.Approved or PayoutStatus.Paid
                && !(await new TeacherFinanceAccountService(_db).GetWithdrawalAsync(payout.TeacherId, ct)).CoversReservedPayment)
                return ApiResponse<bool>.Fail("المتاح للصرف تغير بعد الحجز بسبب مديونية أو تعديل. راجع حساب المدرس قبل الصرف.");

            if (request.Status == PayoutStatus.Approved)
            {
                if (payout.Status != PayoutStatus.Pending)
                {
                    return ApiResponse<bool>.Fail("لا يمكن اعتماد طلب غير معلق");
                }

                if (payout.Amount > account.ReservedBalance)
                {
                    return ApiResponse<bool>.Fail($"رصيد المعلم المحجوز لا يكفي لاعتماد الدفعة بقيمة ({payout.Amount} ج.م)");
                }

                payout.ApprovedByUserId = request.AdminUserId;
                payout.ApprovedAt = DateTime.UtcNow;
            }
            else if (request.Status == PayoutStatus.Paid)
            {
                if (payout.Status != PayoutStatus.Approved)
                {
                    return ApiResponse<bool>.Fail("يجب اعتماد طلب الدفعة قبل تسجيل الصرف الفعلي");
                }

                if (payout.Amount > account.ReservedBalance || payout.Amount > account.CurrentBalance)
                {
                    return ApiResponse<bool>.Fail($"رصيد المعلم المحجوز لا يكفي لصرف الدفعة بقيمة ({payout.Amount} ج.م)");
                }

                account.CurrentBalance -= payout.Amount;
                account.ReservedBalance -= payout.Amount;
                account.UpdatedAt = DateTime.UtcNow;
                payout.PaidByUserId = request.AdminUserId;
                payout.PaidAt = DateTime.UtcNow;
                await _posting.PostAsync(new FinancialPostingRequest("TeacherPayout", payout.Id,
                    "TeacherPayout", $"teacher-payout:{payout.Id:N}:paid", "صرف مستحقات مدرس", payout.PaidAt.Value,
                    request.AdminUserId, [new("2000", payout.Amount, 0m, TeacherId: payout.TeacherId),
                        new("1000", 0m, payout.Amount, TeacherId: payout.TeacherId)]), ct);
            }
            else if (request.Status == PayoutStatus.Rejected)
            {
                if (payout.Amount > account.ReservedBalance)
                {
                    return ApiResponse<bool>.Fail($"رصيد المعلم المحجوز لا يكفي لإلغاء حجز الدفعة بقيمة ({payout.Amount} ج.م)");
                }

                account.ReservedBalance -= payout.Amount;
                account.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                return ApiResponse<bool>.Fail("حالة طلب الدفعة غير مدعومة");
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

            var message = request.Status switch
            {
                PayoutStatus.Approved => "تم اعتماد طلب الدفعة وأصبح جاهزاً للصرف",
                PayoutStatus.Paid => "تم تسجيل صرف الدفعة بنجاح",
                PayoutStatus.Rejected => "تم رفض طلب الدفعة",
                _ => "تم تحديث طلب الدفعة"
            };

            return ApiResponse<bool>.Ok(true, message);
        }
        catch (Exception ex) when (!SerializationRetryHelper.IsSerializationFailure(ex) && IsConcurrencyFailure(ex))
        {
            return ApiResponse<bool>.Fail("تم إجراء عملية متزامنة على هذا الطلب. يرجى المحاولة مرة أخرى.");
        }
    }

    private static bool IsConcurrencyFailure(Exception ex)
    {
        return ex.Message.Contains("concurrent update", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException != null && IsConcurrencyFailure(ex.InnerException));
    }
}
