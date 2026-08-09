using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using Xunit;

namespace NaderGorge.Application.Tests;

public class WalletSmsBalanceUpdaterTests
{
    [Fact]
    public async Task Latest_reported_balance_skips_newer_messages_without_a_balance()
    {
        await using var db = TestAppDbContextFactory.Create();
        var wallet = WalletWithBalance(100m);
        var balanceSms = BalanceSms(wallet, new DateTime(2026, 8, 9, 8, 0, 0, DateTimeKind.Utc), 90718.95m);
        var notification = BalanceSms(wallet, balanceSms.ReceivedAt.AddMinutes(1), 1m);
        notification.Body = "تابع مصروفاتك من تاريخ المعاملات";
        db.AddRange(wallet, balanceSms, notification);
        await db.SaveChangesAsync();

        var latestBalance = await db.ReadLatestReportedBalanceAsync(wallet.Id, CancellationToken.None);

        Assert.Equal(90718.95m, latestBalance);
    }

    [Fact]
    public async Task Delayed_sms_does_not_overwrite_balance_from_a_newer_message()
    {
        await using var db = TestAppDbContextFactory.Create();
        var wallet = WalletWithBalance(73063.95m);
        var delayedSms = BalanceSms(wallet, new DateTime(2026, 8, 9, 8, 0, 0, DateTimeKind.Utc), 70000m);
        var newerSms = BalanceSms(wallet, delayedSms.ReceivedAt.AddMinutes(1), 73063.95m);
        db.AddRange(wallet, newerSms);
        await db.SaveChangesAsync();

        await db.ApplyIfLatestAsync(wallet, delayedSms, null, CancellationToken.None);

        Assert.Equal(73063.95m, wallet.CurrentBalance);
    }

    [Fact]
    public async Task Latest_balance_inquiry_replaces_the_stored_wallet_balance()
    {
        await using var db = TestAppDbContextFactory.Create();
        var wallet = WalletWithBalance(73063.95m);
        db.DigitalWallets.Add(wallet);
        await db.SaveChangesAsync();
        var latestSms = BalanceSms(wallet, new DateTime(2026, 8, 9, 11, 38, 0, DateTimeKind.Utc), 90718.95m);

        await db.ApplyIfLatestAsync(wallet, latestSms, null, CancellationToken.None);

        Assert.Equal(90718.95m, wallet.CurrentBalance);
    }

    private static DigitalWallet WalletWithBalance(decimal balance) => new()
    {
        Label = "Regression wallet",
        PhoneNumber = "01000000000",
        PairingToken = Guid.NewGuid().ToString("N")[..8],
        CurrentBalance = balance
    };

    private static IncomingSmsLog BalanceSms(DigitalWallet wallet, DateTime receivedAt, decimal balance) => new()
    {
        WalletId = wallet.Id,
        Wallet = wallet,
        Sender = "VodafoneCash",
        Body = $"رصيد حسابك فى فودافون كاش الحالي{balance.ToString("0.00", CultureInfo.InvariantCulture)} جنيه",
        ReceivedAt = receivedAt,
        DeduplicationHash = Guid.NewGuid().ToString("N")
    };
}
