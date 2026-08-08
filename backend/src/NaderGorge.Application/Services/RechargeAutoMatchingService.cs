using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class RechargeAutoMatchingService(
    IAppDbContext db,
    BalanceService balanceService,
    ILogger<RechargeAutoMatchingService> logger)
{
    public async Task<int> ReconcilePendingAsync(CancellationToken ct)
    {
        var pendingIds = await db.RechargeRequests
            .AsNoTracking()
            .Where(request => request.Status == RechargeRequestStatus.Pending
                && request.ScreenshotUrl != null && request.ScreenshotUrl != ""
                && request.SenderPhoneNumber != ""
                && request.TeacherId != null)
            .OrderBy(request => request.CreatedAt)
            .Select(request => request.Id)
            .Take(100)
            .ToListAsync(ct);

        var matchedCount = 0;
        foreach (var pendingId in pendingIds)
        {
            if (await TryMatchAsync(pendingId, ct))
                matchedCount++;
        }

        if (matchedCount > 0)
            logger.LogInformation("Automatically reconciled {MatchedCount} pending recharge requests.", matchedCount);

        return matchedCount;
    }

    private async Task<bool> TryMatchAsync(Guid requestId, CancellationToken ct)
    {
        var hasActiveTransaction = db is DbContext context && context.Database.CurrentTransaction != null;
        await using var transaction = hasActiveTransaction
            ? null
            : await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var request = await db.RechargeRequests
                .Include(item => item.Wallet)
                .FirstOrDefaultAsync(item => item.Id == requestId && item.Status == RechargeRequestStatus.Pending, ct);
            if (request is null || string.IsNullOrWhiteSpace(request.ScreenshotUrl) || string.IsNullOrWhiteSpace(request.SenderPhoneNumber))
                return false;

            var matchingAnchor = request.UpdatedAt ?? request.CreatedAt;
            var startTime = matchingAnchor.AddHours(-2);
            var endTime = matchingAnchor.AddHours(2);
            var candidates = await db.IncomingSmsLogs
                .Include(log => log.Wallet)
                .Where(log => log.ParsedAmount == request.Amount
                    && log.ParsedSenderPhone != null
                    && !log.IsMatched
                    && log.ReceivedAt >= startTime
                    && log.ReceivedAt <= endTime)
                .OrderBy(log => log.ReceivedAt)
                .Take(50)
                .ToListAsync(ct);

            var exactCandidates = candidates
                .Where(log => log.ParsedSenderPhone == request.SenderPhoneNumber)
                .Take(2)
                .ToList();

            // Ambiguous evidence must remain for manual review.
            if (exactCandidates.Count != 1)
            {
                var requiresConfirmation = !request.SenderPhoneConfirmedAt.HasValue
                    && candidates.Any(log => RechargePhoneSimilarity.RequiresConfirmation(
                        request.SenderPhoneNumber,
                        log.ParsedSenderPhone));
                if (request.RequiresSenderPhoneConfirmation != requiresConfirmation)
                {
                    request.RequiresSenderPhoneConfirmation = requiresConfirmation;
                    await db.SaveChangesAsync(ct);
                }
                return false;
            }

            var sms = exactCandidates[0];
            var uniqueRequest = await RechargeMatchCandidateSelector.UniquePendingRequestAsync(
                db.RechargeRequests,
                new RechargeMatchKey(request.Amount, request.SenderPhoneNumber, sms.ReceivedAt),
                ct);
            if (uniqueRequest?.Id != request.Id)
                return false;

            var resolvedAt = DateTime.UtcNow;
            request.WalletId = sms.WalletId;
            request.Wallet = sms.Wallet;
            if (!await ReserveMatchAsync(request, sms, resolvedAt, ct))
                return false;

            request.Wallet.CurrentBalance = SmsParser.Parse(sms.Body).CurrentBalance
                ?? request.Wallet.CurrentBalance + request.Amount;
            await db.SaveChangesAsync(ct);

            await balanceService.AddTeacherCredit(
                request.UserId,
                request.TeacherId!.Value,
                request.Amount,
                $"شحن رصيد للمدرس - مطابقة تلقائية مؤجلة (محفظة {request.Wallet.Label})",
                request.UserId,
                request.Id,
                ct);

            if (transaction is not null)
                await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<bool> ReserveMatchAsync(
        RechargeRequest request,
        IncomingSmsLog sms,
        DateTime resolvedAt,
        CancellationToken ct)
    {
        if (db is not DbContext context || context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            request.Status = RechargeRequestStatus.Matched;
            request.ResolvedAt = resolvedAt;
            request.MatchedSmsLogId = sms.Id;
            request.RequiresSenderPhoneConfirmation = false;
            sms.IsMatched = true;
            sms.MatchedRechargeRequestId = request.Id;
            return true;
        }

        var reservedSms = await db.IncomingSmsLogs
            .Where(log => log.Id == sms.Id && !log.IsMatched)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(log => log.IsMatched, true)
                .SetProperty(log => log.MatchedRechargeRequestId, request.Id), ct);
        if (reservedSms != 1)
            return false;

        var reservedRequest = await db.RechargeRequests
            .Where(row => row.Id == request.Id && row.Status == RechargeRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, RechargeRequestStatus.Matched)
                .SetProperty(row => row.ResolvedAt, resolvedAt)
                .SetProperty(row => row.MatchedSmsLogId, sms.Id)
                .SetProperty(row => row.WalletId, sms.WalletId)
                .SetProperty(row => row.RequiresSenderPhoneConfirmation, false), ct);
        return reservedRequest == 1;
    }
}
