using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Content.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Admin;

public sealed class ContentSubscribersTests
{
    [Fact]
    public async Task ListAndExport_ReturnOnePurchaseWinningRowAndExcludeCancelledHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Mixed Student", "01090000001");
        var cancelledOnly = await TestAppDbContextFactory.SeedUserAsync(db, "Cancelled Student", "01090000002");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Gift Admin", "01090000006");
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Gift Teacher", "01090000007");
        var teacher = new TeacherProfile { User = teacherUser, UserId = teacherUser.Id };
        var subject = new Subject { Name = "Subscriber Subject", NormalizedName = "SUBSCRIBER_SUBJECT" };
        var package = new Package
        {
            Name = "Subscriber Package",
            Description = "Package",
            Subject = subject,
            Teacher = teacher,
            TargetGrade = "SecondaryGrade3"
        };
        db.AddRange(teacher, subject, package);
        await db.SaveChangesAsync();
        var packageId = package.Id;
        var purchaseDate = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var issuance = new GiftIssuance
        {
            RequestId = Guid.NewGuid(),
            TargetType = GiftTargetType.Package,
            Package = package,
            PackageId = packageId,
            IssuedByUser = admin,
            IssuedByUserId = admin.Id,
            Reason = "هدية بعد انتهاء الاشتراك"
        };
        var giftRecipient = new GiftRecipient
        {
            GiftIssuance = issuance,
            Student = student,
            StudentId = student.Id,
            Status = GiftRecipientStatus.Active,
            OutcomeCode = "GRANTED"
        };

        db.StudentAccessGrants.AddRange(
            new StudentAccessGrant
            {
                UserId = student.Id,
                GrantType = CodeType.Package,
                PackageId = packageId,
                GrantedAt = purchaseDate,
                ExpiresAt = purchaseDate.AddHours(1),
                IsActive = false
            },
            new StudentAccessGrant
            {
                UserId = student.Id,
                GrantType = CodeType.Package,
                PackageId = packageId,
                GiftRecipient = giftRecipient,
                GrantedAt = purchaseDate.AddDays(1),
                IsActive = true
            },
            new StudentAccessGrant
            {
                UserId = cancelledOnly.Id,
                GrantType = CodeType.Package,
                PackageId = packageId,
                GrantedAt = purchaseDate,
                IsActive = false,
                CancelledAt = purchaseDate.AddHours(1)
            });
        await db.SaveChangesAsync();

        var response = await new GetContentSubscribersQueryHandler(db)
            .Handle(new GetContentSubscribersQuery("package", packageId), CancellationToken.None);
        var csv = await new ExportContentSubscribersQueryHandler(db)
            .Handle(new ExportContentSubscribersQuery("package", packageId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(1, response.Data!.TotalCount);
        var item = Assert.Single(response.Data.Items);
        Assert.Equal(student.Id, item.StudentId);
        Assert.Equal("Direct", item.PurchaseMethod);
        Assert.Equal(purchaseDate, item.EnrolledAt);
        Assert.True(item.IsActive);
        var csvLines = Encoding.UTF8.GetString(csv).TrimStart('\uFEFF').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, csvLines.Length);
        Assert.Contains("مباشر / غير مصنف", csvLines[1]);
        Assert.EndsWith(",نشط", csvLines[1].TrimEnd('\r'));
        Assert.DoesNotContain("Cancelled Student", csvLines[1]);
    }

    [Fact]
    public async Task List_DistinguishesManualGrantFromBalanceFundedPurchase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var packageId = Guid.NewGuid();
        var manualStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Manual Student", "01090000004");
        var balanceStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Balance Student", "01090000005");
        db.StudentAccessGrants.AddRange(
            new StudentAccessGrant { UserId = manualStudent.Id, GrantType = CodeType.Package, PackageId = packageId, IsActive = true },
            new StudentAccessGrant { UserId = balanceStudent.Id, GrantType = CodeType.Package, PackageId = packageId, IsActive = true });
        db.SalesFinancialEffects.Add(new SalesFinancialEffect
        {
            PurchaseOperationId = Guid.NewGuid(),
            StudentId = balanceStudent.Id,
            TargetType = SalesTargetType.Package,
            TargetId = packageId,
            GrossAmount = 100m,
            PromotionalAmount = 100m
        });
        await db.SaveChangesAsync();

        var response = await new GetContentSubscribersQueryHandler(db)
            .Handle(new GetContentSubscribersQuery("package", packageId), CancellationToken.None);

        Assert.Equal(2, response.Data!.TotalCount);
        Assert.Equal("Direct", response.Data.Items.Single(item => item.StudentId == manualStudent.Id).PurchaseMethod);
        Assert.Equal("Balance", response.Data.Items.Single(item => item.StudentId == balanceStudent.Id).PurchaseMethod);
    }

    [Fact]
    public async Task List_KeepsExpiredHistoricalSubscriberButMarksTheRowInactive_OnRelationalProvider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var student = new User { FullName = "Expired Student", PhoneNumber = "01090000003", PasswordHash = "hash" };
        var packageId = Guid.NewGuid();
        db.Users.Add(student);
        db.StudentAccessGrants.AddRange(
            new StudentAccessGrant
            {
                User = student,
                GrantType = CodeType.Package,
                PackageId = packageId,
                GrantedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            },
            new StudentAccessGrant
            {
                User = student,
                GrantType = CodeType.Package,
                PackageId = packageId,
                GrantedAt = DateTime.UtcNow.AddDays(-3),
                IsActive = false
            });
        await db.SaveChangesAsync();

        var response = await new GetContentSubscribersQueryHandler(db)
            .Handle(new GetContentSubscribersQuery("package", packageId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(1, response.Data!.TotalCount);
        Assert.False(Assert.Single(response.Data.Items).IsActive);
    }
}
