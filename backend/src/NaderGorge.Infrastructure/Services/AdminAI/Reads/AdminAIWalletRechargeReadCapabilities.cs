using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIWalletRechargeSummary(int Wallets, int RechargeRequests, int SmsMessages, int TransferReviews, int StudentBalances, int Transactions, DateTime DataAsOf);

public sealed class AdminAIWalletRechargeSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "wallet-recharge.summary";
    public Type OutputType => typeof(AdminAIWalletRechargeSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var value = new AdminAIWalletRechargeSummary(
            await db.DigitalWallets.AsNoTracking().CountAsync(ct),
            await db.RechargeRequests.AsNoTracking().CountAsync(ct),
            await db.IncomingSmsLogs.AsNoTracking().CountAsync(ct),
            await db.WalletTransferReviews.AsNoTracking().CountAsync(ct),
            await db.StudentBalances.AsNoTracking().CountAsync(ct),
            await db.BalanceTransactions.AsNoTracking().CountAsync(ct),
            asOf);
        return new(value, 1, true, false, asOf, ["admin.wallets", "admin.recharges"]);
    }
}
