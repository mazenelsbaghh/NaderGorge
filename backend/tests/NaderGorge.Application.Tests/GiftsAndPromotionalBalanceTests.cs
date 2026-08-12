using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Common;
using NaderGorge.API.Controllers;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Gifts.Commands;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Features.Admin.Gifts.Queries;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class GiftsAndPromotionalBalanceTests
{
    [Fact]
    public void AdminGiftsController_RequiresDedicatedPermission()
    {
        var permission = Assert.Single(typeof(AdminGiftsController)
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true));
        Assert.NotNull(permission);
    }

    [Fact]
    public async Task IssueDirectGift_IsPartialAndIdempotent_WithoutPaidTransaction()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, "Gift Student", "15201");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "15202");
        var package = await SeedPackageAsync(db, 120m);
        var requestId = Guid.NewGuid();
        var request = new IssueGiftRequest(
            requestId,
            GiftTargetType.Package,
            package.Id,
            null,
            null,
            null,
            null,
            new[] { student.Id, student.Id, Guid.NewGuid() },
            "تعويض دعم");
        var handler = new IssueGiftCommandHandler(
            db,
            new AccessCheckService(db),
            new BalanceService(db, NullLogger<BalanceService>.Instance));

        var first = await handler.Handle(new IssueGiftCommand(request, admin.Id), CancellationToken.None);
        var replay = await handler.Handle(new IssueGiftCommand(request, admin.Id), CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal(2, first.Data!.Recipients.Count);
        Assert.Contains(first.Data.Recipients, x => x.StudentId == student.Id && x.Status == GiftRecipientStatus.Active);
        Assert.Contains(first.Data.Recipients, x => x.Status == GiftRecipientStatus.Failed);
        Assert.True(replay.Success);
        Assert.True(replay.Data!.IsReplay);
        Assert.Single(db.GiftIssuances);
        Assert.Single(db.StudentAccessGrants);
        Assert.Empty(db.BalanceTransactions);
        Assert.Contains(db.AuditLogs, x => x.Action == "GiftIssued");
    }

    [Fact]
    public async Task GeneralBalanceGift_CreditsPaidBalance_WithPlatformGiftTransaction()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, "Gift Balance Student", "15208");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "15209");
        var request = new IssueGiftRequest(
            Guid.NewGuid(),
            GiftTargetType.GeneralBalance,
            null,
            null,
            75m,
            null,
            null,
            new[] { student.Id },
            "تعويض حضور");
        var handler = new IssueGiftCommandHandler(
            db,
            new AccessCheckService(db),
            new BalanceService(db, NullLogger<BalanceService>.Instance));

        var result = await handler.Handle(new IssueGiftCommand(request, admin.Id), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(GiftRecipientStatus.Completed, Assert.Single(result.Data!.Recipients).Status);
        var balance = await db.StudentBalances.SingleAsync(x => x.UserId == student.Id);
        Assert.Equal(75m, balance.CurrentBalance);
        var transaction = await db.BalanceTransactions.SingleAsync(x => x.StudentBalanceId == balance.Id);
        Assert.Equal(75m, transaction.Amount);
        Assert.Equal(75m, transaction.BalanceAfter);
        Assert.Equal("PlatformGift", transaction.TransactionType);
        Assert.Equal("هدية من المنصة: تعويض حضور", transaction.Description);
        Assert.Empty(db.PromotionalBalanceAllocations);
    }

    [Fact]
    public async Task GeneralBalanceGift_MixedRecipients_ReportsPartialSuccessAndCorrectAuditCount()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, "Partial Gift Student", "15210");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Partial Gift Admin", "15211");
        var request = new IssueGiftRequest(
            Guid.NewGuid(),
            GiftTargetType.GeneralBalance,
            null,
            null,
            50m,
            null,
            null,
            new[] { student.Id, Guid.NewGuid() },
            "تعويض جزئي");
        var handler = new IssueGiftCommandHandler(
            db,
            new AccessCheckService(db),
            new BalanceService(db, NullLogger<BalanceService>.Instance));

        var result = await handler.Handle(new IssueGiftCommand(request, admin.Id), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(GiftIssuanceStatus.PartiallySuccessful, result.Data!.Status);
        Assert.Contains(result.Data.Recipients, recipient => recipient.Status == GiftRecipientStatus.Completed);
        Assert.Contains(result.Data.Recipients, recipient => recipient.Status == GiftRecipientStatus.Failed);
        var audit = Assert.Single(db.AuditLogs, item => item.Action == "GiftIssued");
        Assert.Contains("\"succeeded\":1", audit.NewValues);
    }

    [Fact]
    public async Task GiftLedgerAndDetails_SumTeacherBalanceOriginalAndAvailableAmountsAcrossRecipients()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Gift Ledger Admin", "152091");
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Gift Ledger Teacher", "152092");
        var firstStudent = await TestAppDbContextFactory.SeedUserAsync(db, "First Recipient", "152093");
        var secondStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Second Recipient", "152094");
        var teacher = new TeacherProfile { UserId = teacherUser.Id, User = teacherUser };
        var issuance = new GiftIssuance
        {
            RequestId = Guid.NewGuid(),
            TargetType = GiftTargetType.TeacherBalance,
            Teacher = teacher,
            TeacherId = teacher.Id,
            Amount = 100m,
            Reason = "تعويض جماعي",
            IssuedByUser = admin,
            IssuedByUserId = admin.Id
        };
        issuance.Recipients.Add(CreateBalanceRecipient(issuance, firstStudent, teacher.Id, 100m, 70m));
        issuance.Recipients.Add(CreateBalanceRecipient(issuance, secondStudent, teacher.Id, 100m, 80m));
        db.AddRange(teacher, issuance);
        await db.SaveChangesAsync();

        var response = await new GetGiftsQueryHandler(db)
            .Handle(new GetGiftsQuery(TargetType: GiftTargetType.TeacherBalance), CancellationToken.None);

        var gift = Assert.Single(response.Data!.Items);
        Assert.Equal(2, gift.SuccessfulCount);
        Assert.Equal(200m, gift.OriginalValue);
        Assert.Equal(150m, gift.AvailableValue);

        var detailsResponse = await new GetGiftDetailsQueryHandler(db)
            .Handle(new GetGiftDetailsQuery(issuance.Id), CancellationToken.None);
        Assert.Equal(200m, detailsResponse.Data!.OriginalValue);
        Assert.Equal(150m, detailsResponse.Data.AvailableValue);
        Assert.Equal(150m, detailsResponse.Data.AvailableAmount);
    }

    [Fact]
    public async Task GiftLedgerAndDetails_ShowGeneralBalanceTotalWithoutInventingAvailableAmount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "General Gift Ledger Admin", "152095");
        var firstStudent = await TestAppDbContextFactory.SeedUserAsync(db, "General First Recipient", "152096");
        var secondStudent = await TestAppDbContextFactory.SeedUserAsync(db, "General Second Recipient", "152097");
        var issuance = new GiftIssuance
        {
            RequestId = Guid.NewGuid(),
            TargetType = GiftTargetType.GeneralBalance,
            Amount = 100m,
            Reason = "رصيد عام جماعي",
            IssuedByUser = admin,
            IssuedByUserId = admin.Id,
            Status = GiftIssuanceStatus.Completed
        };
        issuance.Recipients.Add(new GiftRecipient { Student = firstStudent, StudentId = firstStudent.Id, Status = GiftRecipientStatus.Completed, OutcomeCode = "GRANTED_TO_GENERAL_BALANCE" });
        issuance.Recipients.Add(new GiftRecipient { Student = secondStudent, StudentId = secondStudent.Id, Status = GiftRecipientStatus.Completed, OutcomeCode = "GRANTED_TO_GENERAL_BALANCE" });
        db.GiftIssuances.Add(issuance);
        await db.SaveChangesAsync();

        var response = await new GetGiftsQueryHandler(db)
            .Handle(new GetGiftsQuery(TargetType: GiftTargetType.GeneralBalance), CancellationToken.None);

        var gift = Assert.Single(response.Data!.Items);
        Assert.Equal(200m, gift.OriginalValue);
        Assert.Null(gift.AvailableValue);

        var detailsResponse = await new GetGiftDetailsQueryHandler(db)
            .Handle(new GetGiftDetailsQuery(issuance.Id), CancellationToken.None);
        Assert.Equal(200m, detailsResponse.Data!.OriginalValue);
        Assert.Null(detailsResponse.Data.AvailableValue);
        Assert.Equal(0m, detailsResponse.Data.AvailableAmount);
    }

    [Fact]
    public async Task VideoGift_ExposesOnlySelectedVideo_AndConsumesSuccessfulSession()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedContentFixtureAsync(db);
        var recipient = await SeedGiftVideoGrantAsync(db, fixture.Student.Id, fixture.FirstVideo.Id, maxUses: 1);
        var access = new AccessCheckService(db);

        Assert.True(await access.HasAccessToVideoAsync(fixture.Student.Id, fixture.FirstVideo.Id));
        Assert.False(await access.HasAccessToVideoAsync(fixture.Student.Id, fixture.SecondVideo.Id));

        var detailHandler = new GetLessonDetailQueryHandler(db, access, new TeacherAuthorizationService(db));
        var details = await detailHandler.Handle(new GetLessonDetailQuery(fixture.Lesson.Id, fixture.Student.Id), CancellationToken.None);

        Assert.True(details.Success);
        Assert.True(details.Data!.HasAccess);
        Assert.True(details.Data.IsVideoOnlyAccess);
        Assert.Equal(fixture.FirstVideo.Id, Assert.Single(details.Data.Videos).Id);
        Assert.Null(details.Data.Homework);

        var sessionHandler = new CreateVideoSessionCommandHandler(
            db,
            access,
            FakeEncryption.Instance,
            new GiftUsageService(db));
        var session = await sessionHandler.Handle(
            new CreateVideoSessionCommand(fixture.FirstVideo.Id, fixture.Student.Id),
            CancellationToken.None);

        Assert.True(session.Success, session.Message);
        Assert.Equal(1, recipient.UsesConsumed);
        Assert.False(recipient.AccessGrant!.IsActive);
        Assert.Equal(GiftRecipientStatus.Completed, recipient.Status);
    }

    [Fact]
    public async Task Purchase_UsesSoonestPromotionalAllocationThenPaidBalance_AndConservesValue()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, "Buyer", "15203");
        var package = await SeedPackageAsync(db, 100m);
        var paidBalance = new StudentBalance { UserId = student.Id, CurrentBalance = 40m };
        db.StudentBalances.Add(paidBalance);
        var first = AddAllocation(db, student.Id, package.TeacherId, 30m, DateTime.UtcNow.AddDays(2));
        var second = AddAllocation(db, student.Id, null, 40m, DateTime.UtcNow.AddDays(10));
        await db.SaveChangesAsync();

        var promotional = new PromotionalBalanceService(db);
        var handler = new PurchaseContentCommandHandler(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            promotional,
            new SalesTargetResolver(db),
            new DiscountEngine(db));
        var result = await handler.Handle(
            new PurchaseContentCommand(student.Id, CodeType.Package, package.Id),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(0m, first.AvailableAmount);
        Assert.Equal(30m, first.ConsumedAmount);
        Assert.Equal(0m, second.AvailableAmount);
        Assert.Equal(40m, second.ConsumedAmount);
        Assert.Equal(10m, paidBalance.CurrentBalance);
        Assert.Equal(first.OriginalAmount, first.AvailableAmount + first.ConsumedAmount + first.ExpiredAmount + first.RevokedAmount);
        Assert.Equal(second.OriginalAmount, second.AvailableAmount + second.ConsumedAmount + second.ExpiredAmount + second.RevokedAmount);
        var paidTransaction = Assert.Single(db.BalanceTransactions);
        Assert.Equal(-30m, paidTransaction.Amount);
        Assert.Equal(2, db.PromotionalBalanceUsages.Count());
        Assert.Single(db.StudentAccessGrants);
    }

    [Fact]
    public async Task TeacherRestrictedBalance_IsIneligibleForAnotherTeacher()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, "Restricted", "15204");
        var teacherA = Guid.NewGuid();
        var teacherB = Guid.NewGuid();
        AddAllocation(db, student.Id, teacherA, 50m, null);
        await db.SaveChangesAsync();
        var service = new PromotionalBalanceService(db);

        Assert.Equal(50m, await service.GetEligibleAmountAsync(student.Id, teacherA));
        Assert.Equal(0m, await service.GetEligibleAmountAsync(student.Id, teacherB));
    }

    [Fact]
    public async Task RevokeGift_MovesOnlyAvailableValueAndIsIdempotent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, "Revoke", "15205");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "15206");
        var allocation = AddAllocation(db, student.Id, null, 100m, null);
        allocation.AvailableAmount = 60m;
        allocation.ConsumedAmount = 40m;
        allocation.Status = PromotionalBalanceStatus.PartiallyUsed;
        allocation.GiftRecipient.Status = GiftRecipientStatus.PartiallyUsed;
        await db.SaveChangesAsync();
        var handler = new RevokeGiftCommandHandler(db);

        var first = await handler.Handle(new RevokeGiftCommand(allocation.GiftRecipient.GiftIssuanceId, "خطأ في الإصدار", admin.Id), CancellationToken.None);
        var replay = await handler.Handle(new RevokeGiftCommand(allocation.GiftRecipient.GiftIssuanceId, "إعادة", admin.Id), CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(first.Data!.Changed);
        Assert.Equal(60m, first.Data.RevokedAmount);
        Assert.Equal(40m, allocation.ConsumedAmount);
        Assert.Equal(60m, allocation.RevokedAmount);
        Assert.Equal(0m, allocation.AvailableAmount);
        Assert.True(replay.Success);
        Assert.False(replay.Data!.Changed);
        Assert.Equal(60m, allocation.RevokedAmount);
    }

    private static async Task<User> SeedStudentAsync(AppDbContext db, string name, string phone)
    {
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Type == RoleType.Student);
        if (role == null)
        {
            role = new Role { Name = "Student", Type = RoleType.Student };
            db.Roles.Add(role);
        }
        var user = new User { FullName = name, PhoneNumber = phone, PasswordHash = "hash", IsActive = true };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { User = user, Role = role });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Package> SeedPackageAsync(AppDbContext db, decimal price)
    {
        var subject = new Subject { Name = $"Subject {Guid.NewGuid():N}", NormalizedName = Guid.NewGuid().ToString("N"), Description = "Subject" };
        var package = new Package { Name = "Gift package", Description = "Package", Price = price, IsActive = true, Subject = subject, TargetGrade = "3", TeacherId = Guid.NewGuid() };
        db.AddRange(subject, package);
        await db.SaveChangesAsync();
        return package;
    }

    private static PromotionalBalanceAllocation AddAllocation(AppDbContext db, Guid studentId, Guid? teacherId, decimal amount, DateTime? expiresAt)
    {
        var issuance = new GiftIssuance { RequestId = Guid.NewGuid(), TargetType = teacherId.HasValue ? GiftTargetType.TeacherBalance : GiftTargetType.GeneralBalance, TeacherId = teacherId, Amount = amount, Reason = "Test", IssuedByUserId = Guid.NewGuid() };
        var recipient = new GiftRecipient { GiftIssuance = issuance, StudentId = studentId, Status = GiftRecipientStatus.Active, OutcomeCode = "GRANTED" };
        var allocation = new PromotionalBalanceAllocation { GiftRecipient = recipient, StudentId = studentId, TeacherId = teacherId, OriginalAmount = amount, AvailableAmount = amount, ExpiresAt = expiresAt };
        recipient.PromotionalBalanceAllocation = allocation;
        db.GiftIssuances.Add(issuance);
        db.GiftRecipients.Add(recipient);
        db.PromotionalBalanceAllocations.Add(allocation);
        return allocation;
    }

    private static GiftRecipient CreateBalanceRecipient(
        GiftIssuance issuance,
        User student,
        Guid teacherId,
        decimal originalAmount,
        decimal availableAmount)
    {
        var recipient = new GiftRecipient
        {
            GiftIssuance = issuance,
            Student = student,
            StudentId = student.Id,
            Status = GiftRecipientStatus.Active,
            OutcomeCode = "GRANTED"
        };
        recipient.PromotionalBalanceAllocation = new PromotionalBalanceAllocation
        {
            GiftRecipient = recipient,
            Student = student,
            StudentId = student.Id,
            TeacherId = teacherId,
            OriginalAmount = originalAmount,
            AvailableAmount = availableAmount,
            ConsumedAmount = originalAmount - availableAmount
        };
        return recipient;
    }

    private static async Task<ContentFixture> SeedContentFixtureAsync(AppDbContext db)
    {
        var student = await SeedStudentAsync(db, "Video Student", "15207");
        var subject = new Subject { Name = "Video subject", NormalizedName = "VIDEO_SUBJECT", Description = "Subject" };
        var package = new Package { Name = "Video package", Description = "Package", Subject = subject, TargetGrade = "3", TeacherId = Guid.NewGuid() };
        var term = new Term { Title = "Term", Package = package };
        var section = new ContentSection { Title = "Section", Term = term };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary", ContentSection = section };
        var type = new VideoType { Name = "شرح", NormalizedName = "شرح", IsActive = true };
        var first = new LessonVideo { Title = "Gifted", Provider = "youtube", ProviderVideoId = "first", Lesson = lesson, VideoType = type, IsActive = true };
        var second = new LessonVideo { Title = "Locked", Provider = "youtube", ProviderVideoId = "second", Lesson = lesson, VideoType = type, IsActive = true };
        db.AddRange(subject, package, term, section, lesson, type, first, second);
        await db.SaveChangesAsync();
        return new ContentFixture(student, lesson, first, second);
    }

    private static async Task<GiftRecipient> SeedGiftVideoGrantAsync(AppDbContext db, Guid studentId, Guid videoId, int maxUses)
    {
        var issuance = new GiftIssuance { RequestId = Guid.NewGuid(), TargetType = GiftTargetType.Video, LessonVideoId = videoId, Reason = "Video", IssuedByUserId = Guid.NewGuid(), MaxUses = maxUses };
        var recipient = new GiftRecipient { GiftIssuance = issuance, StudentId = studentId, Status = GiftRecipientStatus.Active, OutcomeCode = "GRANTED" };
        var grant = new StudentAccessGrant { UserId = studentId, GiftRecipient = recipient, GrantType = CodeType.Video, LessonVideoId = videoId, MaxUses = maxUses, IsActive = true };
        recipient.AccessGrant = grant;
        db.AddRange(issuance, recipient, grant);
        await db.SaveChangesAsync();
        return recipient;
    }

    private sealed record ContentFixture(User Student, Lesson Lesson, LessonVideo FirstVideo, LessonVideo SecondVideo);

    private sealed class FakeEncryption : IVideoEncryptionService
    {
        public static readonly FakeEncryption Instance = new();
        public string GenerateSessionKey() => "key";
        public string EncryptVideoInfo(string provider, string videoId, string sessionKey, string? studentName = null, string? studentPhone = null) => "token";
        public (string ProviderName, string ProviderVideoId, string? StudentName, string? StudentPhone) DecryptVideoInfo(string encryptedData, string sessionKey) => ("youtube", "video", null, null);
    }
}
