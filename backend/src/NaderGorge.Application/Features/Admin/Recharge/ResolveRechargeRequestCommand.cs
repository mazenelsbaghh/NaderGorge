using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
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
    Guid? SmsLogId = null,
    Guid? WalletId = null) : IRequest<ApiResponse<bool>>;

public class ResolveRechargeRequestCommandHandler : IRequestHandler<ResolveRechargeRequestCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly BalanceService _balanceService;
    private readonly IFinancialPostingService? _financialPosting;

    public ResolveRechargeRequestCommandHandler(IAppDbContext db, BalanceService balanceService, IFinancialPostingService? financialPosting = null)
    {
        _db = db;
        _balanceService = balanceService;
        _financialPosting = financialPosting;
    }

    public async Task<ApiResponse<bool>> Handle(ResolveRechargeRequestCommand request, CancellationToken ct)
    {
        return await SerializationRetryHelper.ExecuteAsync(
            retryCt => HandleOnce(request, retryCt),
            ct);
    }

    private async Task<ApiResponse<bool>> HandleOnce(ResolveRechargeRequestCommand request, CancellationToken ct)
    {
        await RechargeRequestExpiryService.RejectPendingOlderThan48Hours(_db, ct);

        var rechargeRequest = await _db.RechargeRequests
            .Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.Id == request.RechargeRequestId, ct);

        if (rechargeRequest == null)
            return ApiResponse<bool>.Fail("طلب الشحن غير موجود");

        if (rechargeRequest.Status == RechargeRequestStatus.Approved
            && request.Approve
            && request.WalletId.HasValue
            && request.WalletId.Value != rechargeRequest.WalletId)
        {
            return await CorrectApprovedWalletAsync(rechargeRequest, request.WalletId.Value, request.AdminId, ct);
        }

        if (rechargeRequest.Status is not (RechargeRequestStatus.Pending or RechargeRequestStatus.Rejected))
            return ApiResponse<bool>.Fail("لا يمكن تعديل قرار طلب الشحن في حالته الحالية.");

        if (string.IsNullOrWhiteSpace(rechargeRequest.ScreenshotUrl) || string.IsNullOrWhiteSpace(rechargeRequest.SenderPhoneNumber))
            return ApiResponse<bool>.Fail("لا يمكن معالجة طلب الشحن قبل رفع صورة إثبات التحويل وكتابة رقم المحول منه.");

        var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
        await using var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var resolvedAt = DateTime.UtcNow;

            if (request.Approve)
            {
                IncomingSmsLog? smsLog = null;
                DigitalWallet targetWallet = rechargeRequest.Wallet;

                if (request.SmsLogId.HasValue)
                {
                    smsLog = await _db.IncomingSmsLogs.FirstOrDefaultAsync(l => l.Id == request.SmsLogId.Value, ct);
                    if (smsLog == null)
                        return ApiResponse<bool>.Fail("رسالة التأكيد المحددة غير موجودة");

                    if (smsLog.IsMatched)
                        return ApiResponse<bool>.Fail("تم مطابقة رسالة التأكيد هذه مع طلب آخر مسبقاً");

                    if (request.WalletId.HasValue && request.WalletId.Value != smsLog.WalletId)
                        return ApiResponse<bool>.Fail("المحفظة المختارة لا تطابق المحفظة التي استقبلت رسالة التأكيد.");

                    targetWallet = await _db.DigitalWallets.FirstAsync(wallet => wallet.Id == smsLog.WalletId, ct);

                    smsLog.IsMatched = true;
                    smsLog.MatchedRechargeRequestId = rechargeRequest.Id;
                    rechargeRequest.MatchedSmsLogId = smsLog.Id;
                }
                else if (request.WalletId.HasValue && request.WalletId.Value != rechargeRequest.WalletId)
                {
                    targetWallet = await _db.DigitalWallets
                        .FirstOrDefaultAsync(wallet => wallet.Id == request.WalletId.Value && wallet.IsActive, ct)
                        ?? throw new InvalidOperationException("المحفظة المختارة غير موجودة أو غير نشطة.");
                }

                rechargeRequest.WalletId = targetWallet.Id;
                rechargeRequest.Wallet = targetWallet;

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
                targetWallet.CurrentBalance = linkedSmsBalance ?? targetWallet.CurrentBalance + rechargeRequest.Amount;

                await _db.SaveChangesAsync(ct);

                if (rechargeRequest.TeacherId.HasValue)
                {
                    await _balanceService.AddTeacherCredit(rechargeRequest.UserId, rechargeRequest.TeacherId.Value,
                        rechargeRequest.Amount, $"شحن رصيد للمدرس - موافقة الإدارة (محفظة {targetWallet.Label})",
                        request.AdminId, ct);
                }
                else
                {
                    await _balanceService.AddCredit(rechargeRequest.UserId, rechargeRequest.Amount,
                        $"شحن رصيد عام - موافقة الإدارة (محفظة {targetWallet.Label})",
                        rechargeRequest.Id, "RechargeCredit", ct);
                }

                if (_financialPosting is not null)
                {
                    var treasuryCode = await (from treasury in _db.TreasuryAccounts
                                              join account in _db.FinancialAccounts on treasury.FinancialAccountId equals account.Id
                                              where treasury.DigitalWalletId == targetWallet.Id
                                              select account.Code).SingleOrDefaultAsync(ct) ?? "1000";
                    await _financialPosting.PostAsync(new FinancialPostingRequest(
                        "RechargeRequest", rechargeRequest.Id, "RechargeReceived", $"recharge:{rechargeRequest.Id:N}:approved",
                        rechargeRequest.TeacherId.HasValue ? "شحن رصيد مدرس" : "شحن رصيد عام",
                        resolvedAt, request.AdminId,
                        [new FinancialPostingLine(treasuryCode, rechargeRequest.Amount, 0m, StudentId: rechargeRequest.UserId),
                         new FinancialPostingLine(rechargeRequest.TeacherId.HasValue ? "1110" : "1100", 0m, rechargeRequest.Amount, StudentId: rechargeRequest.UserId, TeacherId: rechargeRequest.TeacherId)]), ct);
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
                .Where(row => row.Id == rechargeRequest.Id
                    && (row.Status == RechargeRequestStatus.Pending || row.Status == RechargeRequestStatus.Rejected))
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

    private async Task<ApiResponse<bool>> CorrectApprovedWalletAsync(
        RechargeRequest rechargeRequest,
        Guid walletId,
        Guid adminId,
        CancellationToken ct)
    {
        if (rechargeRequest.MatchedSmsLogId.HasValue)
            return ApiResponse<bool>.Fail("الطلب المرتبط برسالة SMS يأخذ محفظته من الرسالة ولا يمكن تغييرها يدوياً.");

        var targetWallet = await _db.DigitalWallets
            .FirstOrDefaultAsync(wallet => wallet.Id == walletId && wallet.IsActive, ct);
        if (targetWallet == null)
            return ApiResponse<bool>.Fail("المحفظة المختارة غير موجودة أو غير نشطة.");

        var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
        await using var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            rechargeRequest.Wallet.CurrentBalance = Math.Max(0m, rechargeRequest.Wallet.CurrentBalance - rechargeRequest.Amount);
            targetWallet.CurrentBalance += rechargeRequest.Amount;
            rechargeRequest.WalletId = targetWallet.Id;
            rechargeRequest.Wallet = targetWallet;
            rechargeRequest.ResolvedByUserId = adminId;
            rechargeRequest.ResolvedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            if (transaction != null)
                await transaction.CommitAsync(ct);
            return ApiResponse<bool>.Ok(true, "تم تصحيح محفظة التحويل للطلب المقبول يدوياً.");
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
