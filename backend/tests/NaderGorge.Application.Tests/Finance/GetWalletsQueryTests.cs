using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Wallets;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class GetWalletsQueryTests
{
    [Fact]
    public async Task ProductionRegression_ApprovedTransferCountsTowardItsWalletLimitsOnly()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Wallet Student", "01000000001");
        var (dayStartUtc, dayEndUtc) = CairoTime.GetCurrentDayRangeUtc();
        var resolvedAt = dayStartUtc.AddTicks((dayEndUtc - dayStartUtc).Ticks / 2);

        var firstWallet = new DigitalWallet
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "01000000002",
            Label = "First wallet",
            DailyLimit = 30_000m,
            MonthlyLimit = 100_000m,
            CurrentBalance = 9_500m
        };
        var secondWallet = new DigitalWallet
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "01000000003",
            Label = "Second wallet",
            DailyLimit = 30_000m,
            MonthlyLimit = 100_000m,
            CurrentBalance = 4_000m
        };

        db.DigitalWallets.AddRange(firstWallet, secondWallet);
        db.RechargeRequests.AddRange(
            new RechargeRequest
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WalletId = firstWallet.Id,
                Amount = 100m,
                Status = RechargeRequestStatus.Approved,
                ResolvedAt = resolvedAt,
                SenderPhoneNumber = "01000000004"
            },
            new RechargeRequest
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WalletId = secondWallet.Id,
                Amount = 250m,
                Status = RechargeRequestStatus.Matched,
                ResolvedAt = resolvedAt,
                SenderPhoneNumber = "01000000005"
            });
        await db.SaveChangesAsync();

        var result = await new GetWalletsQueryHandler(db).Handle(new GetWalletsQuery(), CancellationToken.None);

        Assert.True(result.Success);
        var firstWalletResult = Assert.Single(result.Data!, wallet => wallet.Id == firstWallet.Id);
        var secondWalletResult = Assert.Single(result.Data!, wallet => wallet.Id == secondWallet.Id);
        Assert.Equal(100m, firstWalletResult.DailyReceived);
        Assert.Equal(100m, firstWalletResult.MonthlyReceived);
        Assert.Equal(100m, firstWalletResult.TotalReceived);
        Assert.Equal(250m, secondWalletResult.DailyReceived);
        Assert.Equal(250m, secondWalletResult.MonthlyReceived);
        Assert.Equal(250m, secondWalletResult.TotalReceived);
        Assert.Equal(9_500m, firstWalletResult.CurrentBalance);
    }
}
