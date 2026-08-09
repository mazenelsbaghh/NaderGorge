using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public static class WalletSmsBalanceUpdater
{
    public static async Task<decimal?> ReadLatestReportedBalanceAsync(
        this IAppDbContext db,
        Guid walletId,
        CancellationToken ct)
    {
        var messages = db.IncomingSmsLogs.AsNoTracking()
            .Where(log => log.WalletId == walletId)
            .OrderByDescending(log => log.ReceivedAt)
            .ThenByDescending(log => log.CreatedAt)
            .Select(log => log.Body);

        await foreach (var body in messages.AsAsyncEnumerable().WithCancellation(ct))
        {
            var balance = SmsParser.Parse(body).CurrentBalance;
            if (balance.HasValue)
                return balance.Value;
        }

        return null;
    }

    public static async Task ApplyIfLatestAsync(
        this IAppDbContext db,
        DigitalWallet wallet,
        IncomingSmsLog sms,
        decimal? fallbackCredit,
        CancellationToken ct)
    {
        var newerMessageExists = await db.IncomingSmsLogs
            .AnyAsync(log => log.WalletId == wallet.Id && log.ReceivedAt > sms.ReceivedAt, ct);
        if (newerMessageExists)
            return;

        var reportedBalance = SmsParser.Parse(sms.Body).CurrentBalance;
        if (reportedBalance.HasValue)
            wallet.CurrentBalance = reportedBalance.Value;
        else if (fallbackCredit.HasValue)
            wallet.CurrentBalance += fallbackCredit.Value;
    }
}
