using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Recharge;

public sealed record CancelRechargeRequestCommand(Guid UserId, Guid RechargeRequestId, string Reason)
    : IRequest<ApiResponse<bool>>;

public sealed class CancelRechargeRequestCommandHandler(IAppDbContext db)
    : IRequestHandler<CancelRechargeRequestCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CancelRechargeRequestCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<bool>.Fail("اكتب سبب الإلغاء", ["CANCELLATION_REASON_REQUIRED"]);
        var reason = request.Reason.Trim();
        if (reason.Length is < 3 or > 500)
            return ApiResponse<bool>.Fail("سبب الإلغاء يجب أن يكون بين 3 وخمسمائة حرف", ["CANCELLATION_REASON_INVALID"]);

        var rechargeRequest = await db.RechargeRequests.SingleOrDefaultAsync(item =>
            item.Id == request.RechargeRequestId && item.UserId == request.UserId, ct);
        if (rechargeRequest?.Status != RechargeRequestStatus.Pending)
            return ApiResponse<bool>.Fail("لا يمكن إلغاء هذا الطلب بعد حسمه", ["RECHARGE_REQUEST_NOT_CANCELLABLE"]);

        rechargeRequest.Status = RechargeRequestStatus.Cancelled;
        rechargeRequest.RejectionReason = reason;
        rechargeRequest.ResolvedAt = DateTime.UtcNow;
        rechargeRequest.ReservationExpiresAt = null;
        await db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "تم إلغاء طلب الشحن");
    }
}
