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
        return await SerializationRetryHelper.ExecuteAsync(
            retryCt => HandleOnce(request, retryCt),
            ct);
    }

    private async Task<ApiResponse<bool>> HandleOnce(ResolveRechargeRequestCommand request, CancellationToken ct)
    {
        await RechargeRequestExpiryService.RejectPendingOlderThan24Hours(_db, ct);

        var rechargeRequest = await _db.RechargeRequests
            .Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.Id == request.RechargeRequestId, ct);

        if (rechargeRequest == null)
            return ApiResponse<bool>.Fail("طلب الشحن غير موجود");

        if (rechargeRequest.Status != RechargeRequestStatus.Pending)
            return ApiResponse<bool>.Fail("طلب الشحن هذا غير معلق أو تم معالجته مسبقاً");

        if (string.IsNullOrWhiteSpace(rechargeRequest.ScreenshotUrl) || string.IsNullOrWhiteSpace(rechargeRequest.SenderPhoneNumber))
            return ApiResponse<bool>.Fail("لا يمكن معالجة طلب الشحن قبل رفع صورة إثبات التحويل وكتابة رقم المحول منه.");

        var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
        var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var resolvedAt = DateTime.UtcNow;

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

                var transition = await TryTransitionRechargeAsync(
                    rechargeRequest,
                    RechargeRequestStatus.Approved,
                    request.AdminId,
                    resolvedAt,
                    smsLog?.Id,
                    null,
                    ct);
                if (!transition.Success)
                    return transition;

                var linkedSmsBalance = smsLog == null ? null : SmsParser.Parse(smsLog.Body).CurrentBalance;
                rechargeRequest.Wallet.CurrentBalance = linkedSmsBalance ?? rechargeRequest.Wallet.CurrentBalance + rechargeRequest.Amount;

                await _db.SaveChangesAsync(ct);

                if (rechargeRequest.TeacherId.HasValue)
                {
                    await _balanceService.AddTeacherCredit(rechargeRequest.UserId, rechargeRequest.TeacherId.Value,
                        rechargeRequest.Amount, $"شحن رصيد للمدرس - موافقة الإدارة (محفظة {rechargeRequest.Wallet.Label})",
                        request.AdminId, ct);
                }
                else
                {
                    await _balanceService.AddCredit(rechargeRequest.UserId, rechargeRequest.Amount,
                        $"شحن رصيد عام - موافقة الإدارة (محفظة {rechargeRequest.Wallet.Label})",
                        rechargeRequest.Id, "DigitalRecharge", ct);
                }
            }
            else
            {
                var transition = await TryTransitionRechargeAsync(
                    rechargeRequest,
                    RechargeRequestStatus.Rejected,
                    request.AdminId,
                    resolvedAt,
                    null,
                    request.RejectionReason ?? "تم الرفض بواسطة الإدارة",
                    ct);
                if (!transition.Success)
                    return transition;

                await _db.SaveChangesAsync(ct);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            return ApiResponse<bool>.Ok(true, request.Approve ? "تمت الموافقة على طلب الشحن بنجاح" : "تم رفض طلب الشحن");
        }
        catch (Exception ex) when (SerializationRetryHelper.IsSerializationFailure(ex))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
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

    private async Task<ApiResponse<bool>> TryTransitionRechargeAsync(
        RechargeRequest rechargeRequest,
        RechargeRequestStatus nextStatus,
        Guid adminId,
        DateTime resolvedAt,
        Guid? matchedSmsLogId,
        string? rejectionReason,
        CancellationToken ct)
    {
        if (_db is DbContext efDb && efDb.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            var affectedRows = await _db.RechargeRequests
                .Where(row => row.Id == rechargeRequest.Id && row.Status == RechargeRequestStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, nextStatus)
                    .SetProperty(row => row.ResolvedByUserId, adminId)
                    .SetProperty(row => row.ResolvedAt, resolvedAt)
                    .SetProperty(row => row.MatchedSmsLogId, matchedSmsLogId)
                    .SetProperty(row => row.RejectionReason, rejectionReason), ct);

            if (affectedRows != 1)
                return ApiResponse<bool>.Fail("طلب الشحن هذا غير معلق أو تم معالجته مسبقاً");
        }

        rechargeRequest.Status = nextStatus;
        rechargeRequest.ResolvedByUserId = adminId;
        rechargeRequest.ResolvedAt = resolvedAt;
        rechargeRequest.MatchedSmsLogId = matchedSmsLogId;
        rechargeRequest.RejectionReason = rejectionReason;
        return ApiResponse<bool>.Ok(true);
    }
}
