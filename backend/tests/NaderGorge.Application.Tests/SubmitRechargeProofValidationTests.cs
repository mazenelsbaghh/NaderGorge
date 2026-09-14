using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Student.Recharge;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class SubmitRechargeProofValidationTests
{
    // Regression for the 2026-07-22 production failure where browser-normalized WEBP proofs were validated as PNG.
    [Fact]
    public async Task WebpProof_WithMatchingMetadata_PassesImageValidation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = CreateHandler(db);

        var response = await handler.Handle(
            new SubmitRechargeCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "01012345678",
                CreateWebpHeader(),
                "proof.webp",
                "image/webp"),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("طلب الشحن هذا غير موجود", response.Message);
    }

    [Fact]
    public async Task WebpProof_DisguisedAsPng_IsRejected()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = CreateHandler(db);

        var response = await handler.Handle(
            new SubmitRechargeCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "01012345678",
                CreateWebpHeader(),
                "proof.png",
                "image/png"),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("صورة إثبات التحويل يجب أن تكون صورة JPG أو PNG أو WEBP صالحة.", response.Message);
    }

    [Fact]
    public async Task Student_cannot_submit_a_legacy_general_recharge()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000214");
        var wallet = new DigitalWallet
        {
            Label = "محفظة اختبار",
            PhoneNumber = "01010000214",
            IsActive = true,
            DailyLimit = 10_000m,
            MonthlyLimit = 100_000m
        };
        var recharge = new RechargeRequest
        {
            UserId = student.Id,
            User = student,
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = 100m,
            Status = RechargeRequestStatus.Pending,
            ScreenshotUrl = "/proof.webp"
        };
        db.AddRange(wallet, recharge);
        await db.SaveChangesAsync();

        var response = await CreateHandler(db).Handle(
            new SubmitRechargeCommand(
                student.Id,
                recharge.Id,
                "01012345678",
                [],
                string.Empty,
                null),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("لا يمكن استكمال طلب شحن عام", response.Message);
        Assert.Equal(RechargeRequestStatus.Pending, recharge.Status);
    }

    // Regression for the 2026-08-11 Production incident where an accepted request
    // was shown as an error when the browser repeated its proof submission.
    [Theory]
    [InlineData(RechargeRequestStatus.Matched, "تمت مطابقة التحويل وإضافة الرصيد بالفعل.")]
    [InlineData(RechargeRequestStatus.Approved, "تمت الموافقة على طلب الشحن وإضافة الرصيد بالفعل.")]
    public async Task RepeatedProofForAcceptedRequest_ReturnsCurrentSuccess(
        RechargeRequestStatus acceptedStatus,
        string expectedMessage)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000216");
        var wallet = new DigitalWallet
        {
            Label = "محفظة اختبار",
            PhoneNumber = "01010000216",
            IsActive = true,
            DailyLimit = 10_000m,
            MonthlyLimit = 100_000m
        };
        var recharge = new RechargeRequest
        {
            UserId = student.Id,
            User = student,
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = 1_350m,
            SenderPhoneNumber = "01012345678",
            ScreenshotUrl = "/proof.webp",
            Status = acceptedStatus,
            ResolvedAt = DateTime.UtcNow
        };
        db.AddRange(wallet, recharge);
        await db.SaveChangesAsync();

        var response = await CreateHandler(db).Handle(
            new SubmitRechargeCommand(
                student.Id,
                recharge.Id,
                recharge.SenderPhoneNumber,
                [],
                string.Empty,
                null),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.IsMatched);
        Assert.Equal(expectedMessage, response.Message);
        Assert.Equal(expectedMessage, response.Data.Message);
        Assert.Equal(acceptedStatus, recharge.Status);
    }

    // Regression for the 2026-08-09 Production incident where the uniqueness
    // query could not see a proof that had not yet been persisted.
    [Fact]
    public async Task ExistingSms_WhenProofIsSubmitted_MatchesImmediately()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01000000215");
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01100000215");
        var teacher = new TeacherProfile
        {
            UserId = teacherUser.Id,
            User = teacherUser,
            Bio = "Bio",
            Specialization = "Math",
            ContactInfo = teacherUser.PhoneNumber
        };
        var wallet = new DigitalWallet
        {
            Label = "محفظة اختبار",
            PhoneNumber = "01010000215",
            PairingToken = Guid.NewGuid().ToString("N"),
            IsActive = true,
            DailyLimit = 10_000m,
            MonthlyLimit = 100_000m
        };
        var submittedAt = DateTime.UtcNow;
        var recharge = new RechargeRequest
        {
            UserId = student.Id,
            User = student,
            WalletId = wallet.Id,
            Wallet = wallet,
            TeacherId = teacher.Id,
            Teacher = teacher,
            Amount = 490m,
            Status = RechargeRequestStatus.Pending,
            ReservationExpiresAt = submittedAt.AddMinutes(30)
        };
        var sms = new IncomingSmsLog
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Sender = "VodafoneCash",
            Body = "تم استلام مبلغ 490 ج.م",
            ReceivedAt = submittedAt.AddMinutes(-2),
            ParsedAmount = 490m,
            ParsedSenderPhone = "01012345678",
            DeduplicationHash = Guid.NewGuid().ToString("N")
        };
        db.AddRange(teacher, wallet, recharge, sms);
        await db.SaveChangesAsync();

        var handler = new SubmitRechargeCommandHandler(
            db,
            new StubImageStorage("/proof.webp"),
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var response = await handler.Handle(
            new SubmitRechargeCommand(
                student.Id,
                recharge.Id,
                "01012345678",
                CreateWebpHeader(),
                "proof.webp",
                "image/webp"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.IsMatched);
        Assert.Equal(RechargeRequestStatus.Matched, recharge.Status);
        Assert.True(sms.IsMatched);
    }

    private static SubmitRechargeCommandHandler CreateHandler(Infrastructure.Data.AppDbContext db) =>
        new(
            db,
            new UnusedImageStorage(),
            new BalanceService(db, NullLogger<BalanceService>.Instance));

    private static byte[] CreateWebpHeader() =>
    [
        0x52, 0x49, 0x46, 0x46,
        0x04, 0x00, 0x00, 0x00,
        0x57, 0x45, 0x42, 0x50
    ];

    private sealed class UnusedImageStorage : IContentImageStorage
    {
        public Task<string> SaveAsWebpAsync(
            Stream imageStream,
            string contentFolder,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Storage must not be reached for a missing recharge request.");
    }

    private sealed class StubImageStorage(string imageUrl) : IContentImageStorage
    {
        public Task<string> SaveAsWebpAsync(
            Stream imageStream,
            string contentFolder,
            CancellationToken cancellationToken) => Task.FromResult(imageUrl);
    }
}
