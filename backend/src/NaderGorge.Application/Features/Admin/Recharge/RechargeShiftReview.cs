using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Recharge;

public sealed record GetRechargeShiftReviewQuery(
    DateTime From,
    DateTime To,
    Guid? WalletId = null,
    Guid? ResolvedByUserId = null) : IRequest<ApiResponse<RechargeShiftReviewDto>>;

public sealed record RechargeShiftReviewItemDto(
    Guid RechargeRequestId,
    Guid StudentId,
    string StudentName,
    string StudentPhoneNumber,
    decimal Amount,
    string BalanceScope,
    string? TeacherName,
    decimal? BalanceBefore,
    decimal? BalanceAfter,
    decimal CurrentBalance,
    string AcceptanceMethod,
    DateTime ResolvedAt,
    Guid? ResolvedByUserId,
    string ResolvedByUserName,
    Guid WalletId,
    string WalletLabel,
    string WalletPhoneNumber,
    string SenderPhoneNumber,
    Guid? MatchedSmsLogId,
    bool SuspectedDuplicate,
    string? DuplicateReason,
    bool IsReversed,
    bool CanReverse,
    string? ReverseBlockedReason);

public sealed record RechargeShiftReviewDto(
    IReadOnlyList<RechargeShiftReviewItemDto> Items,
    int AcceptedCount,
    int ManualCount,
    int AutomaticCount,
    int SuspectedDuplicateCount,
    decimal TotalAmount);

public sealed class GetRechargeShiftReviewQueryHandler(IAppDbContext db)
    : IRequestHandler<GetRechargeShiftReviewQuery, ApiResponse<RechargeShiftReviewDto>>
{
    public async Task<ApiResponse<RechargeShiftReviewDto>> Handle(GetRechargeShiftReviewQuery request, CancellationToken ct)
    {
        if (request.To <= request.From || request.To - request.From > TimeSpan.FromDays(31))
            return ApiResponse<RechargeShiftReviewDto>.Fail("فترة التقرير غير صالحة أو أكبر من 31 يوماً.");

        var acceptedStatuses = new[] { RechargeRequestStatus.Approved, RechargeRequestStatus.Matched };
        var query = db.RechargeRequests.AsNoTracking()
            .Where(row => acceptedStatuses.Contains(row.Status)
                && row.ResolvedAt >= request.From && row.ResolvedAt < request.To);
        if (request.WalletId.HasValue)
            query = query.Where(row => row.WalletId == request.WalletId.Value);
        if (request.ResolvedByUserId.HasValue)
            query = query.Where(row => row.ResolvedByUserId == request.ResolvedByUserId.Value);

        var rows = await query
            .OrderByDescending(row => row.ResolvedAt)
            .Select(row => new
            {
                Request = row,
                StudentName = row.User.FullName,
                StudentPhone = row.User.PhoneNumber,
                WalletLabel = row.Wallet.Label,
                WalletPhone = row.Wallet.PhoneNumber,
                WalletBalance = row.Wallet.CurrentBalance,
                TeacherName = row.Teacher != null ? row.Teacher.User.FullName : null,
                ResolverName = row.ResolvedByUser != null ? row.ResolvedByUser.FullName : "النظام"
            })
            .ToListAsync(ct);

        var requestIds = rows.Select(row => row.Request.Id).ToArray();
        var studentIds = rows.Select(row => row.Request.UserId).Distinct().ToArray();
        var generalBalances = await db.StudentBalances.AsNoTracking()
            .Where(balance => studentIds.Contains(balance.UserId))
            .ToDictionaryAsync(balance => balance.UserId, balance => balance.CurrentBalance, ct);
        var rechargeTransactions = await db.BalanceTransactions.AsNoTracking()
            .Where(transaction => transaction.ReferenceId.HasValue && requestIds.Contains(transaction.ReferenceId.Value)
                && (transaction.TransactionType == "RechargeCredit" || transaction.TransactionType == "RechargeReversal"))
            .ToListAsync(ct);
        var teacherIssuances = await db.GiftIssuances.AsNoTracking()
            .Where(issuance => requestIds.Contains(issuance.RequestId) && issuance.TargetType == GiftTargetType.TeacherBalance)
            .Select(issuance => new
            {
                issuance.RequestId,
                Allocation = issuance.Recipients.Select(recipient => recipient.PromotionalBalanceAllocation).FirstOrDefault()
            })
            .ToListAsync(ct);

        var teacherKeys = rows.Where(row => row.Request.TeacherId.HasValue)
            .Select(row => new { row.Request.UserId, TeacherId = row.Request.TeacherId!.Value }).Distinct().ToArray();
        var teacherStudentIds = teacherKeys.Select(key => key.UserId).Distinct().ToArray();
        var teacherIds = teacherKeys.Select(key => key.TeacherId).Distinct().ToArray();
        var scopedBalances = await db.PromotionalBalanceAllocations.AsNoTracking()
            .Where(allocation => teacherStudentIds.Contains(allocation.StudentId)
                && allocation.TeacherId.HasValue && teacherIds.Contains(allocation.TeacherId.Value)
                && allocation.Status != PromotionalBalanceStatus.Revoked)
            .GroupBy(allocation => new { allocation.StudentId, TeacherId = allocation.TeacherId!.Value })
            .Select(group => new { group.Key.StudentId, group.Key.TeacherId, Balance = group.Sum(item => item.AvailableAmount) })
            .ToListAsync(ct);

        var duplicateGroups = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Request.SenderPhoneNumber))
            .GroupBy(row => $"{Digits(row.Request.SenderPhoneNumber)}:{row.Request.Amount}:{row.Request.WalletId}")
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(row => row.Request.Id))
            .ToHashSet();

        var items = rows.Select(row =>
        {
            var recharge = row.Request;
            var isTeacherBalance = recharge.TeacherId.HasValue;
            var credit = rechargeTransactions.FirstOrDefault(tx => tx.ReferenceId == recharge.Id && tx.TransactionType == "RechargeCredit");
            var linkedIssuance = teacherIssuances.FirstOrDefault(item => item.RequestId == recharge.Id)?.Allocation;
            var reversal = rechargeTransactions.FirstOrDefault(tx => tx.ReferenceId == recharge.Id && tx.TransactionType == "RechargeReversal");
            var isReversed = isTeacherBalance
                ? linkedIssuance?.Status == PromotionalBalanceStatus.Revoked
                : reversal is not null;
            var currentBalance = isTeacherBalance
                ? scopedBalances.FirstOrDefault(balance => balance.StudentId == recharge.UserId && balance.TeacherId == recharge.TeacherId) ?.Balance ?? 0m
                : generalBalances.GetValueOrDefault(recharge.UserId);
            var hasSafeSource = isTeacherBalance ? linkedIssuance is not null : credit is not null;
            var enoughStudentBalance = isTeacherBalance
                ? linkedIssuance is not null && linkedIssuance.AvailableAmount >= recharge.Amount
                : currentBalance >= recharge.Amount;
            var enoughWalletBalance = row.WalletBalance >= recharge.Amount;
            var blockedReason = isReversed ? "تم عكس هذا الشحن مسبقاً"
                : !hasSafeSource ? "لا يوجد ربط مؤكد بقيد الرصيد لهذا الطلب القديم"
                : !enoughStudentBalance ? "الرصيد المتاح استُخدم ولا يكفي للعكس بدون عجز"
                : !enoughWalletBalance ? "رصيد المحفظة المسجل لا يكفي للعكس"
                : null;
            return new RechargeShiftReviewItemDto(
                recharge.Id, recharge.UserId, row.StudentName, row.StudentPhone, recharge.Amount,
                isTeacherBalance ? "رصيد مدرس" : "رصيد عام", row.TeacherName,
                isTeacherBalance ? linkedIssuance?.OriginalAmount - recharge.Amount : credit?.BalanceAfter - recharge.Amount,
                isTeacherBalance ? linkedIssuance?.OriginalAmount : credit?.BalanceAfter,
                currentBalance,
                recharge.Status == RechargeRequestStatus.Matched ? "آلي" : "يدوي",
                recharge.ResolvedAt!.Value, recharge.ResolvedByUserId, row.ResolverName,
                recharge.WalletId, row.WalletLabel, row.WalletPhone, recharge.SenderPhoneNumber,
                recharge.MatchedSmsLogId, duplicateGroups.Contains(recharge.Id),
                duplicateGroups.Contains(recharge.Id) ? "نفس رقم المحول والمبلغ والمحفظة تكرر في الفترة" : null,
                isReversed, blockedReason is null, blockedReason);
        }).ToArray();

        return ApiResponse<RechargeShiftReviewDto>.Ok(new RechargeShiftReviewDto(
            items, items.Length, items.Count(item => item.AcceptanceMethod == "يدوي"),
            items.Count(item => item.AcceptanceMethod == "آلي"), items.Count(item => item.SuspectedDuplicate),
            items.Sum(item => item.Amount)));
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());
}

public sealed record ReverseRechargeCreditCommand(
    Guid RechargeRequestId,
    Guid ActorUserId,
    string Reason,
    bool PreserveWalletBalance = false)
    : IRequest<ApiResponse<bool>>;

public sealed class ReverseRechargeCreditCommandHandler(
    IAppDbContext db,
    IFinancialPostingService? financialPosting = null)
    : IRequestHandler<ReverseRechargeCreditCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ReverseRechargeCreditCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<bool>.Fail("سبب العكس المالي مطلوب.");

        var hasActiveTransaction = db is DbContext efDb && efDb.Database.CurrentTransaction != null;
        await using var transaction = hasActiveTransaction
            ? null
            : await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var recharge = await db.RechargeRequests
                .Include(row => row.Wallet)
                .Include(row => row.MatchedSmsLog).ThenInclude(log => log!.Wallet)
                .SingleOrDefaultAsync(row => row.Id == request.RechargeRequestId, ct);
            if (recharge is null || recharge.Status is not (RechargeRequestStatus.Approved or RechargeRequestStatus.Matched))
                return ApiResponse<bool>.Fail("طلب الشحن غير موجود أو غير مقبول.");
            var receivingWallet = recharge.MatchedSmsLog?.Wallet ?? recharge.Wallet;
            if (!request.PreserveWalletBalance && receivingWallet.CurrentBalance < recharge.Amount)
                return ApiResponse<bool>.Fail("رصيد المحفظة المسجل لا يسمح بالعكس بدون عجز.");

            decimal balanceAfter;
            StudentBalance? generalBalance = null;
            if (recharge.TeacherId.HasValue)
            {
                var issuance = await db.GiftIssuances
                    .Include(item => item.Recipients).ThenInclude(recipient => recipient.PromotionalBalanceAllocation)
                    .SingleOrDefaultAsync(item => item.RequestId == recharge.Id && item.TargetType == GiftTargetType.TeacherBalance, ct);
                var recipient = issuance?.Recipients.SingleOrDefault();
                var allocation = recipient?.PromotionalBalanceAllocation;
                if (issuance is null || recipient is null || allocation is null)
                    return ApiResponse<bool>.Fail("هذا طلب قديم بلا ربط مؤكد بتخصيص رصيد المدرس؛ يحتاج مراجعة يدوية.");
                if (issuance.Status == GiftIssuanceStatus.Revoked || allocation.Status == PromotionalBalanceStatus.Revoked)
                    return ApiResponse<bool>.Fail("تم عكس هذا الشحن مسبقاً.");
                if (allocation.AvailableAmount < recharge.Amount)
                    return ApiResponse<bool>.Fail("تم استخدام جزء من الرصيد ولا يمكن عكسه بدون إنشاء عجز.");

                allocation.AvailableAmount -= recharge.Amount;
                allocation.RevokedAmount += recharge.Amount;
                allocation.Status = allocation.AvailableAmount == 0 ? PromotionalBalanceStatus.Revoked : PromotionalBalanceStatus.PartiallyUsed;
                recipient.Status = allocation.AvailableAmount == 0 ? GiftRecipientStatus.Revoked : GiftRecipientStatus.PartiallyUsed;
                recipient.RevokedAt = DateTime.UtcNow;
                recipient.RevokedByUserId = request.ActorUserId;
                recipient.RevocationReason = request.Reason.Trim();
                issuance.Status = GiftIssuanceStatus.Revoked;
                balanceAfter = allocation.AvailableAmount;
            }
            else
            {
                if (await db.BalanceTransactions.AnyAsync(tx => tx.ReferenceId == recharge.Id && tx.TransactionType == "RechargeReversal", ct))
                    return ApiResponse<bool>.Fail("تم عكس هذا الشحن مسبقاً.");
                var creditExists = await db.BalanceTransactions.AnyAsync(tx => tx.ReferenceId == recharge.Id
                    && (tx.TransactionType == "RechargeCredit" || tx.TransactionType == "DigitalRecharge"), ct);
                if (!creditExists)
                    return ApiResponse<bool>.Fail("لا يوجد قيد شحن مؤكد مرتبط بالطلب.");
                generalBalance = await db.StudentBalances.SingleOrDefaultAsync(item => item.UserId == recharge.UserId, ct);
                if (generalBalance is null || generalBalance.CurrentBalance < recharge.Amount)
                    return ApiResponse<bool>.Fail("رصيد الطالب الحالي لا يكفي للعكس بدون عجز.");
                generalBalance.CurrentBalance -= recharge.Amount;
                generalBalance.Version++;
                balanceAfter = generalBalance.CurrentBalance;
            }

            if (!request.PreserveWalletBalance)
                receivingWallet.CurrentBalance -= recharge.Amount;
            if (generalBalance is not null)
            {
                db.BalanceTransactions.Add(new BalanceTransaction
                {
                    StudentBalanceId = generalBalance.Id,
                    Amount = -recharge.Amount,
                    BalanceAfter = balanceAfter,
                    TransactionType = "RechargeReversal",
                    ReferenceId = recharge.Id,
                    Description = $"عكس شحن مكرر: {request.Reason.Trim()}",
                    PerformedByUserId = request.ActorUserId
                });
            }
            db.OutboxEvents.Add(new OutboxEvent
            {
                Type = "BalanceChanged",
                TargetUserId = recharge.UserId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { newBalance = balanceAfter, rechargeReversed = true })
            });

            var journal = await db.JournalEntries.SingleOrDefaultAsync(entry => entry.SourceType == "RechargeRequest"
                && entry.SourceId == recharge.Id && entry.Status == JournalEntryStatus.Posted, ct);
            if (journal is not null && financialPosting is not null)
                await financialPosting.ReverseAsync(journal.Id, request.ActorUserId, request.Reason.Trim(), ct);

            await db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
            return ApiResponse<bool>.Ok(true, "تم عكس الشحن والقيد المالي بدون إنشاء عجز.");
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
        }
    }

}
