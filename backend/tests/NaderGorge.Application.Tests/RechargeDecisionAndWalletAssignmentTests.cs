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
    public async Task Student_cancels_pending_request_and_admin_can_see_the_reason()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000021");
        var wallet = Wallet("01010000021");
        var pending = PendingRequest(user, wallet, 100m);
        db.AddRange(wallet, pending);
        await db.SaveChangesAsync();

        var cancelled = await new CancelRechargeRequestCommandHandler(db).Handle(
            new CancelRechargeRequestCommand(user.Id, pending.Id, "أنشأت الطلب بالخطأ"), CancellationToken.None);
        var adminRequests = await new GetAdminRechargeRequestsQueryHandler(db).Handle(
            new GetAdminRechargeRequestsQuery(RechargeRequestStatus.Cancelled), CancellationToken.None);

        Assert.True(cancelled.Success);
        Assert.Equal(RechargeRequestStatus.Cancelled, pending.Status);
        Assert.Null(pending.ReservationExpiresAt);
        Assert.Equal("أنشأت الطلب بالخطأ", Assert.Single(adminRequests.Data!).RejectionReason);
    }

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
    public async Task Initiate_rejects_a_second_request_while_one_is_pending()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000001");
        var teacher = await SeedRechargeTeacherAsync(db, "01100000001");
        var wallet = Wallet("01010000001");
        var pending = PendingRequest(user, wallet, 100m, teacher.Id);
        db.AddRange(wallet, pending);
        await db.SaveChangesAsync();

        var result = await new InitiateRechargeCommandHandler(db)
            .Handle(new InitiateRechargeCommand(user.Id, 150m, teacher.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("طلب شحن معلق", result.Message);
        Assert.Equal(100m, pending.Amount);
        Assert.Equal(1, await db.RechargeRequests.CountAsync());
    }

    [Fact]
    public async Task Initiate_allows_a_new_request_after_the_student_cancels_the_pending_one()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000011");
        var teacher = await SeedRechargeTeacherAsync(db, "01100000011");
        var wallet = Wallet("01010000011");
        var pending = PendingRequest(user, wallet, 100m, teacher.Id);
        db.AddRange(wallet, pending);
        await db.SaveChangesAsync();

        var cancelled = await new CancelRechargeRequestCommandHandler(db).Handle(
            new CancelRechargeRequestCommand(user.Id, pending.Id, "ألغاه الطالب لإنشاء طلب جديد"),
            CancellationToken.None);

        var result = await new InitiateRechargeCommandHandler(db)
            .Handle(new InitiateRechargeCommand(user.Id, 150m, teacher.Id), CancellationToken.None);

        Assert.True(cancelled.Success);
        Assert.True(result.Success);
        Assert.Equal(RechargeRequestStatus.Cancelled, pending.Status);
        Assert.Equal(2, await db.RechargeRequests.CountAsync());
        Assert.Single(await db.RechargeRequests.Where(item => item.Status == RechargeRequestStatus.Pending).ToListAsync());
    }

    [Fact]
    public async Task Student_history_hides_an_inactive_wallet_phone_number()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000012");
        var wallet = Wallet("01010000012");
        wallet.IsActive = false;
        db.AddRange(wallet, PendingRequest(user, wallet, 100m));
        await db.SaveChangesAsync();

        var result = await new GetMyRechargeRequestsQueryHandler(db)
            .Handle(new GetMyRechargeRequestsQuery(user.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, Assert.Single(result.Data!).WalletPhoneNumber);
    }

    [Fact]
    public async Task Reconciliation_matches_one_exact_sms_using_the_latest_request_update()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000013");
        var teacher = await SeedRechargeTeacherAsync(db, "01100000013");
        var wallet = Wallet("01010000013");
        var pending = PendingRequest(user, wallet, 200m, teacher.Id);
        pending.CreatedAt = DateTime.UtcNow.AddHours(-20);
        pending.SenderPhoneNumber = "01099999991";
        pending.ScreenshotUrl = "/proof.webp";
        db.AddRange(wallet, pending);
        await db.SaveChangesAsync();
        pending.UpdatedAt = DateTime.UtcNow;
        var sms = new IncomingSmsLog
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Sender = "VodafoneCash",
            Body = "تم استلام مبلغ 200 ج.م من 01099999991",
            ReceivedAt = pending.UpdatedAt.Value.AddMinutes(5),
            ParsedAmount = 200m,
            ParsedSenderPhone = "01099999991",
            DeduplicationHash = Guid.NewGuid().ToString("N")
        };
        db.IncomingSmsLogs.Add(sms);
        await db.SaveChangesAsync();

        var matcher = new RechargeAutoMatchingService(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            NullLogger<RechargeAutoMatchingService>.Instance);
        var matched = await matcher.ReconcilePendingAsync(CancellationToken.None);

        Assert.Equal(1, matched);
        Assert.Equal(RechargeRequestStatus.Matched, pending.Status);
        Assert.True(sms.IsMatched);
        var allocation = await db.PromotionalBalanceAllocations.SingleAsync();
        Assert.Equal(teacher.Id, allocation.TeacherId);
        Assert.Equal(200m, allocation.AvailableAmount);
    }

    [Fact]
    public async Task Initiate_does_not_move_an_existing_pending_request_to_another_wallet()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000002");
        var teacher = await SeedRechargeTeacherAsync(db, "01100000002");
        var firstWallet = Wallet("01010000002", dailyLimit: 100m);
        var secondWallet = Wallet("01010000003", dailyLimit: 1_000m);
        var pending = PendingRequest(user, firstWallet, 50m, teacher.Id);
        var consumed = new RechargeRequest
        {
            UserId = user.Id,
            User = user,
            WalletId = firstWallet.Id,
            Wallet = firstWallet,
            Amount = 80m,
            TeacherId = teacher.Id,
            Status = RechargeRequestStatus.Approved,
            SenderPhoneNumber = "01099999999",
            ScreenshotUrl = "/proof.webp"
        };
        db.AddRange(firstWallet, secondWallet, pending, consumed);
        await db.SaveChangesAsync();

        var result = await new InitiateRechargeCommandHandler(db)
            .Handle(new InitiateRechargeCommand(user.Id, 50m, teacher.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(firstWallet.Id, pending.WalletId);
        Assert.Equal(1, await db.RechargeRequests.CountAsync(request => request.Status == RechargeRequestStatus.Pending));
    }

    [Theory]
    [InlineData("01012345678", "01012345699", 9)]
    [InlineData("99123456780", "00123456789", 8)]
    [InlineData("01012345678", "01598765432", 2)]
    public void Phone_similarity_uses_eight_or_more_contiguous_digits_anywhere(
        string submitted,
        string received,
        int expectedLongestSequence)
    {
        Assert.Equal(expectedLongestSequence, RechargePhoneSimilarity.LongestCommonDigitSequence(submitted, received));
        Assert.Equal(expectedLongestSequence >= 8, RechargePhoneSimilarity.RequiresConfirmation(submitted, received));
    }

    [Fact]
    public async Task Reconciliation_requests_student_confirmation_for_a_near_phone_without_crediting_balance()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000031");
        var teacher = await SeedRechargeTeacherAsync(db, "01100000031");
        var wallet = Wallet("01010000031");
        var pending = PendingRequest(user, wallet, 200m, teacher.Id);
        pending.SenderPhoneNumber = "01012345678";
        pending.OriginalSenderPhoneNumber = pending.SenderPhoneNumber;
        pending.ScreenshotUrl = "/proof.webp";
        var sms = new IncomingSmsLog
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Sender = "VodafoneCash",
            Body = "تم استلام مبلغ 200 ج.م",
            ReceivedAt = DateTime.UtcNow,
            ParsedAmount = 200m,
            ParsedSenderPhone = "01012345699",
            DeduplicationHash = Guid.NewGuid().ToString("N")
        };
        db.AddRange(wallet, pending, sms);
        await db.SaveChangesAsync();

        var matcher = new RechargeAutoMatchingService(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            NullLogger<RechargeAutoMatchingService>.Instance);
        var matched = await matcher.ReconcilePendingAsync(CancellationToken.None);

        Assert.Equal(0, matched);
        Assert.Equal(RechargeRequestStatus.Pending, pending.Status);
        Assert.True(pending.RequiresSenderPhoneConfirmation);
        Assert.False(sms.IsMatched);
        Assert.Empty(await db.PromotionalBalanceAllocations.ToListAsync());
    }

    [Fact]
    public async Task Rejected_request_can_be_approved_on_the_wallet_that_received_the_transfer()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000003");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01000000004");
        var teacher = await SeedRechargeTeacherAsync(db, "01100000003");
        var originalWallet = Wallet("01010000004");
        var receivingWallet = Wallet("01010000005");
        var recharge = PendingRequest(student, originalWallet, 200m, teacher.Id);
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
        var allocation = await db.PromotionalBalanceAllocations.SingleAsync();
        Assert.Equal(teacher.Id, allocation.TeacherId);
        Assert.Equal(200m, allocation.AvailableAmount);
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

    [Fact]
    public async Task Initiate_allows_a_general_recharge_without_a_teacher()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000014");
        db.DigitalWallets.Add(Wallet("01010000014"));
        await db.SaveChangesAsync();

        var result = await new InitiateRechargeCommandHandler(db)
            .Handle(new InitiateRechargeCommand(user.Id, 100m), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null((await db.RechargeRequests.SingleAsync()).TeacherId);
    }

    private static async Task<TeacherProfile> SeedRechargeTeacherAsync(NaderGorge.Infrastructure.Data.AppDbContext db, string phoneNumber)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Recharge Teacher", phoneNumber);
        var teacher = new TeacherProfile { UserId = user.Id, User = user, Bio = "Bio", Specialization = "Math", ContactInfo = phoneNumber };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();
        return teacher;
    }

    private static RechargeRequest PendingRequest(User user, DigitalWallet wallet, decimal amount, Guid? teacherId = null) => new()
    {
        UserId = user.Id,
        User = user,
        WalletId = wallet.Id,
        Wallet = wallet,
        Amount = amount,
        TeacherId = teacherId,
        Status = RechargeRequestStatus.Pending,
        ReservationExpiresAt = DateTime.UtcNow.AddMinutes(30)
    };
}
