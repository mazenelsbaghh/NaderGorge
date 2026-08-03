using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Admin.Recharge;
using NaderGorge.Application.Features.Student.Recharge;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class RechargeDecisionAndWalletAssignmentTests
{
    [Fact]
    public async Task Pending_request_stays_open_for_two_days_then_expires()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000000");
        var wallet = Wallet("01010000000");
        var withinWindow = PendingRequest(user, wallet, 50m);
        withinWindow.CreatedAt = DateTime.UtcNow.AddHours(-25);
        var outsideWindow = PendingRequest(user, wallet, 60m);
        outsideWindow.CreatedAt = DateTime.UtcNow.AddHours(-49);
        db.AddRange(wallet, withinWindow, outsideWindow);
        await db.SaveChangesAsync();

        await RechargeRequestExpiryService.RejectPendingOlderThan48Hours(db, CancellationToken.None);

        Assert.Equal(RechargeRequestStatus.Pending, withinWindow.Status);
        Assert.Equal(RechargeRequestStatus.Rejected, outsideWindow.Status);
        Assert.Equal(RechargeRequestExpiryService.AutoRejectionReason, outsideWindow.RejectionReason);
    }

    [Fact]
    public async Task Initiate_reuses_the_same_wallet_for_an_unfinished_request()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000001");
        var wallet = Wallet("01010000001");
        var pending = PendingRequest(user, wallet, 100m);
        db.AddRange(wallet, pending);
        await db.SaveChangesAsync();

        var result = await new InitiateRechargeCommandHandler(db)
            .Handle(new InitiateRechargeCommand(user.Id, 150m), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(pending.Id, result.Data!.RechargeRequestId);
        Assert.Equal(wallet.PhoneNumber, result.Data.WalletPhoneNumber);
        Assert.Equal(150m, pending.Amount);
        Assert.Equal(1, await db.RechargeRequests.CountAsync());
    }

    [Fact]
    public async Task Initiate_changes_wallet_only_when_the_sticky_wallet_has_reached_its_limit()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000002");
        var firstWallet = Wallet("01010000002", dailyLimit: 100m);
        var secondWallet = Wallet("01010000003", dailyLimit: 1_000m);
        var pending = PendingRequest(user, firstWallet, 50m);
        var consumed = new RechargeRequest
        {
            UserId = user.Id,
            User = user,
            WalletId = firstWallet.Id,
            Wallet = firstWallet,
            Amount = 80m,
            Status = RechargeRequestStatus.Approved,
            SenderPhoneNumber = "01099999999",
            ScreenshotUrl = "/proof.webp"
        };
        db.AddRange(firstWallet, secondWallet, pending, consumed);
        await db.SaveChangesAsync();

        var result = await new InitiateRechargeCommandHandler(db)
            .Handle(new InitiateRechargeCommand(user.Id, 50m), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(secondWallet.Id, pending.WalletId);
        Assert.Equal(secondWallet.PhoneNumber, result.Data!.WalletPhoneNumber);
        Assert.Equal(1, await db.RechargeRequests.CountAsync(request => request.Status == RechargeRequestStatus.Pending));
    }

    [Fact]
    public async Task Rejected_request_can_be_approved_on_the_wallet_that_received_the_transfer()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000003");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01000000004");
        var originalWallet = Wallet("01010000004");
        var receivingWallet = Wallet("01010000005");
        var recharge = PendingRequest(student, originalWallet, 200m);
        recharge.Status = RechargeRequestStatus.Rejected;
        recharge.SenderPhoneNumber = "01088888888";
        recharge.ScreenshotUrl = "/proof.webp";
        recharge.RejectionReason = "صورة غير واضحة";
        db.AddRange(originalWallet, receivingWallet, recharge);
        await db.SaveChangesAsync();

        var handler = new ResolveRechargeRequestCommandHandler(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var result = await handler.Handle(
            new ResolveRechargeRequestCommand(recharge.Id, true, admin.Id, WalletId: receivingWallet.Id),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(RechargeRequestStatus.Approved, recharge.Status);
        Assert.Equal(receivingWallet.Id, recharge.WalletId);
        Assert.Null(recharge.RejectionReason);
        Assert.Equal(200m, (await db.StudentBalances.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Manual_approval_wallet_can_be_corrected_without_crediting_the_student_twice()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000005");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01000000006");
        var originalWallet = Wallet("01010000006");
        var receivingWallet = Wallet("01010000007");
        originalWallet.CurrentBalance = 300m;
        var recharge = PendingRequest(student, originalWallet, 300m);
        recharge.Status = RechargeRequestStatus.Approved;
        recharge.SenderPhoneNumber = "01077777777";
        recharge.ScreenshotUrl = "/proof.webp";
        db.AddRange(originalWallet, receivingWallet, recharge, new StudentBalance { UserId = student.Id, CurrentBalance = 300m });
        await db.SaveChangesAsync();

        var handler = new ResolveRechargeRequestCommandHandler(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var result = await handler.Handle(
            new ResolveRechargeRequestCommand(recharge.Id, true, admin.Id, WalletId: receivingWallet.Id),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(receivingWallet.Id, recharge.WalletId);
        Assert.Equal(0m, originalWallet.CurrentBalance);
        Assert.Equal(300m, receivingWallet.CurrentBalance);
        Assert.Equal(300m, (await db.StudentBalances.SingleAsync()).CurrentBalance);
        Assert.Empty(await db.BalanceTransactions.ToListAsync());
    }

    private static DigitalWallet Wallet(string phoneNumber, decimal dailyLimit = 10_000m) => new()
    {
        PhoneNumber = phoneNumber,
        Label = phoneNumber,
        DailyLimit = dailyLimit,
        MonthlyLimit = 100_000m,
        IsActive = true
    };

    private static RechargeRequest PendingRequest(User user, DigitalWallet wallet, decimal amount) => new()
    {
        UserId = user.Id,
        User = user,
        WalletId = wallet.Id,
        Wallet = wallet,
        Amount = amount,
        Status = RechargeRequestStatus.Pending,
        ReservationExpiresAt = DateTime.UtcNow.AddMinutes(30)
    };
}
