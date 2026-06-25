using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Recharge;

public record ResolveRechargeRequestCommand(
    Guid RechargeRequestId,
    bool Approve,
    Guid AdminId,
    string? RejectionReason = null,
    Guid? SmsLogId = null) : IRequest<ApiResponse<bool>>;

public class ResolveRechargeRequestCommandHandler : IRequestHandler<ResolveRechargeRequestCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly BalanceService _balanceService;

    public ResolveRechargeRequestCommandHandler(IAppDbContext db, BalanceService balanceService)
    {
        _db = db;
        _balanceService = balanceService;
    }

    public async Task<ApiResponse<bool>> Handle(ResolveRechargeRequestCommand request, CancellationToken ct)
    {
        await RechargeRequestExpiryService.RejectPendingOlderThan24Hours(_db, ct);

        var rechargeRequest = await _db.RechargeRequests
            .Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.Id == request.RechargeRequestId, ct);

        if (rechargeRequest == null)
            return ApiResponse<bool>.Fail("طلب الشحن غير موجود");

        if (rechargeRequest.Status != RechargeRequestStatus.Pending)
            return ApiResponse<bool>.Fail("طلب الشحن هذا غير معلق أو تم معالجته مسبقاً");

        var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
        var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        try
        {
            rechargeRequest.ResolvedByUserId = request.AdminId;
            rechargeRequest.ResolvedAt = DateTime.UtcNow;

            if (request.Approve)
            {
                IncomingSmsLog? smsLog = null;

                if (request.SmsLogId.HasValue)
                {
                    smsLog = await _db.IncomingSmsLogs.FirstOrDefaultAsync(l => l.Id == request.SmsLogId.Value, ct);
                    if (smsLog == null)
                        return ApiResponse<bool>.Fail("رسالة التأكيد المحددة غير موجودة");

                    if (smsLog.IsMatched)
                        return ApiResponse<bool>.Fail("تم مطابقة رسالة التأكيد هذه مع طلب آخر مسبقاً");

                    smsLog.IsMatched = true;
                    smsLog.MatchedRechargeRequestId = rechargeRequest.Id;
                    rechargeRequest.MatchedSmsLogId = smsLog.Id;
                }

                rechargeRequest.Status = RechargeRequestStatus.Approved;
                var linkedSmsBalance = smsLog == null ? null : SmsParser.Parse(smsLog.Body).CurrentBalance;
                rechargeRequest.Wallet.CurrentBalance = linkedSmsBalance ?? rechargeRequest.Wallet.CurrentBalance + rechargeRequest.Amount;

                await _db.SaveChangesAsync(ct);

                // Credit the student's balance
                await _balanceService.AddCredit(
                    rechargeRequest.UserId,
                    rechargeRequest.Amount,
                    $"شحن رصيد يدوي - موافقة الإدارة (محفظة {rechargeRequest.Wallet.Label})",
                    rechargeRequest.Id,
                    "DigitalRecharge",
                    ct);
            }
            else
            {
                rechargeRequest.Status = RechargeRequestStatus.Rejected;
                rechargeRequest.RejectionReason = request.RejectionReason ?? "تم الرفض بواسطة الإدارة";
                await _db.SaveChangesAsync(ct);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            return ApiResponse<bool>.Ok(true, request.Approve ? "تمت الموافقة على طلب الشحن بنجاح" : "تم رفض طلب الشحن");
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            return ApiResponse<bool>.Fail($"فشل في معالجة طلب الشحن: {ex.Message}");
        }
    }
}
