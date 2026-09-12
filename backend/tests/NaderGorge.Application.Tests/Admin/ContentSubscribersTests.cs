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
    [Theory]
    [InlineData("package", 15)]
    [InlineData("term", 15)]
    [InlineData("section", 1)]
    [InlineData("lesson", 1)]
    public async Task SubscribersUnderChildLevels_AppearInParentListAndExportWithoutDuplicates(
        string contentType, int expectedCount)
    {
        // September 2026 regression: package summary showed 15 term buyers, but its list was empty.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01093000000");
        var teacher = new TeacherProfile { User = teacherUser, UserId = teacherUser.Id };
        var subject = new Subject { Name = "Subject", NormalizedName = "SUBSCRIBERS_SUBJECT" };
        var package = new Package { Name = "Parent Package", Teacher = teacher, Subject = subject, TargetGrade = "SecondaryGrade3" };
        var otherPackage = new Package { Name = "Other Package", Teacher = teacher, Subject = subject, TargetGrade = "SecondaryGrade3" };
        db.AddRange(package, otherPackage);
        await db.SaveChangesAsync();
        var packageId = package.Id;
        var otherPackageId = otherPackage.Id;
        var term = new Term { PackageId = packageId, Title = "First Term" };
        var otherTerm = new Term { PackageId = otherPackageId, Title = "Other Term" };
        var section = new ContentSection { Term = term, Title = "Section" };
        var lesson = new Lesson { ContentSection = section, Title = "Lesson" };
        db.AddRange(term, otherTerm, section, lesson);
        var grantedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var students = new List<User>();
        for (var index = 0; index < 15; index++)
        {
            var student = await TestAppDbContextFactory.SeedUserAsync(db, $"Buyer {index:D2}", $"0109100{index:D4}");
            students.Add(student);
            db.StudentAccessGrants.Add(new StudentAccessGrant
            {
                User = student, GrantType = CodeType.Term, TermId = term.Id,
                GrantedAt = grantedAt, IsActive = true
            });
            db.SalesFinancialEffects.Add(new SalesFinancialEffect
            {
                PurchaseOperationId = Guid.NewGuid(), StudentId = student.Id,
                TargetType = SalesTargetType.Term, TargetId = term.Id, GrossAmount = 100m, PaidAmount = 100m
            });
        }
        var excluded = await TestAppDbContextFactory.SeedUserAsync(db, "Excluded Student", "01092000000");
        db.StudentAccessGrants.AddRange(
            new StudentAccessGrant { User = students[0], GrantType = CodeType.Lesson, LessonId = lesson.Id, GrantedAt = grantedAt.AddDays(1) },
            new StudentAccessGrant { User = excluded, GrantType = CodeType.Term, TermId = otherTerm.Id },
            new StudentAccessGrant { User = excluded, GrantType = CodeType.Term, TermId = term.Id, CancelledAt = grantedAt });
        await db.SaveChangesAsync();
        var contentId = contentType switch
        {
            "package" => packageId, "term" => term.Id, "section" => section.Id, _ => lesson.Id
        };
        var handler = new GetContentSubscribersQueryHandler(db);

        var response = await handler.Handle(new GetContentSubscribersQuery(contentType, contentId), CancellationToken.None);
        var csv = Encoding.UTF8.GetString(await new ExportContentSubscribersQueryHandler(db)
            .Handle(new ExportContentSubscribersQuery(contentType, contentId), CancellationToken.None));

        Assert.True(response.Success);
        Assert.Equal(expectedCount, response.Data!.TotalCount);
        Assert.Equal(expectedCount, response.Data.Items.Select(row => row.StudentId).Distinct().Count());
        Assert.DoesNotContain(response.Data.Items, row => row.StudentId == excluded.Id);
        var manualLesson = Assert.Single(response.Data.Items, row => row.StudentId == students[0].Id);
        Assert.Equal("Lesson", manualLesson.PurchaseType);
        Assert.Equal("Direct", manualLesson.PurchaseMethod);
        Assert.All(response.Data.Items.Where(row => row.StudentId != students[0].Id), row => Assert.Equal("Balance", row.PurchaseMethod));
        Assert.Equal(expectedCount + 1, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("Excluded Student", csv);
        Assert.Contains(",حصة,مباشر / غير مصنف,", csv);

        var searched = await handler.Handle(new GetContentSubscribersQuery(contentType, contentId, Search: "Buyer 00"), CancellationToken.None);
        Assert.Equal(students[0].Id, Assert.Single(searched.Data!.Items).StudentId);
        if (expectedCount > 10)
        {
            var firstPage = await handler.Handle(new GetContentSubscribersQuery(contentType, contentId, PageSize: 10), CancellationToken.None);
            var secondPage = await handler.Handle(new GetContentSubscribersQuery(contentType, contentId, Page: 2, PageSize: 10), CancellationToken.None);
            Assert.Equal(10, firstPage.Data!.Items.Count);
            Assert.Equal(5, secondPage.Data!.Items.Count);
            Assert.Equal(15, firstPage.Data.Items.Concat(secondPage.Data.Items).Select(row => row.StudentId).Distinct().Count());
        }
    }

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
