using NaderGorge.Application.Features.Teacher.Finance.Queries;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NaderGorge.Application.Tests;

public class TeacherAccountingPhase3Tests
{
    [Fact]
    [Trait("Category", "Finance")]
    public async Task RecordEventAsync_WhenCalledTwiceWithSameKey_CreditsTeacherOnce()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Ledger Teacher", "01030000001");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Ledger Student", "01030000002");
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.20m,
            Specialization = "Math",
            ContactInfo = "phone"
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();

        var service = new TeacherAccountingService(db);
        var input = new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase,
            Guid.NewGuid(),
            student.Id,
            SalesTargetType.Lesson,
            Guid.NewGuid(),
            100m,
            0m,
            100m,
            0m,
            80m,
            "test:teacher-ledger-once",
            "{}",
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(
                    teacher.Id,
                    TeacherAllocationMode.CommissionRate,
                    0.20m,
                    100m,
                    20m,
                    80m,
                    student.FullName,
                    student.PhoneNumber,
                    "Lesson")
            });

        await service.RecordEventAsync(input, CancellationToken.None);
        await service.RecordEventAsync(input, CancellationToken.None);

        Assert.Single(db.TeacherFinancialEvents);
        Assert.Single(db.TeacherFinancialAllocations);
        var account = Assert.Single(db.TeacherAccounts);
        Assert.Equal(20m, account.TotalEarnings);
        Assert.Equal(20m, account.CurrentBalance);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task CancelPackageGrantCommand_WhenGrantIsTerm_ReversesTeacherEarnings()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Term Cancel Teacher", "01030000011");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Term Cancel Student", "01030000012");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Term Cancel Admin", "01030000013");

        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.50m,
            Specialization = "Math",
            ContactInfo = "phone"
        };
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Name = "Term Cancel Subject",
            NormalizedName = $"TERM_CANCEL_{Guid.NewGuid():N}",
            Description = "Test Subject"
        };
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Term Cancel Package",
            Description = "Test Package",
            Price = 200m,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            TargetGrade = "3rd Secondary"
        };
        var term = new Term
        {
            Id = Guid.NewGuid(),
            Title = "First Term",
            Order = 1,
            Price = 100m,
            PackageId = package.Id
        };
        var grant = new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            GrantType = CodeType.Term,
            PackageId = package.Id,
            TermId = term.Id,
            IsActive = true
        };

        db.TeacherProfiles.Add(teacher);
        db.Subjects.Add(subject);
        db.Packages.Add(package);
        db.Terms.Add(term);
        db.StudentAccessGrants.Add(grant);
        await db.SaveChangesAsync();

        var service = new TeacherAccountingService(db);
        await service.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase,
            Guid.NewGuid(),
            student.Id,
            SalesTargetType.Term,
            term.Id,
            100m,
            0m,
            100m,
            0m,
            50m,
            "test:term-grant-cancellation",
            "{}",
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(
                    teacher.Id,
                    TeacherAllocationMode.CommissionRate,
                    0.50m,
                    100m,
                    50m,
                    50m,
                    student.FullName,
                    student.PhoneNumber,
                    "First Term")
            }), CancellationToken.None);

        var handler = new CancelPackageGrantCommandHandler(db, service);
        var result = await handler.Handle(
            new CancelPackageGrantCommand(grant.Id, false, admin.Id, "Term access cancelled"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False((await db.StudentAccessGrants.SingleAsync(g => g.Id == grant.Id)).IsActive);

        var account = await db.TeacherAccounts.SingleAsync();
        Assert.Equal(0m, account.TotalEarnings);
        Assert.Equal(0m, account.CurrentBalance);

        var reversalAllocation = await db.TeacherFinancialAllocations
            .SingleAsync(a => a.TeacherShareAmount < 0m);
        Assert.Equal(-50m, reversalAllocation.TeacherShareAmount);
        Assert.Equal(TeacherAllocationMode.Reversal, reversalAllocation.AllocationMode);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task RecordEventAsync_ZeroValueEvent_DoesNotIncreaseTeacherBalance()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Free Teacher", "01030000003");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Free Student", "01030000004");
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.20m,
            Specialization = "Math",
            ContactInfo = "phone"
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();

        var service = new TeacherAccountingService(db);
        await service.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase,
            Guid.NewGuid(),
            student.Id,
            SalesTargetType.Lesson,
            Guid.NewGuid(),
            100m,
            100m,
            0m,
            0m,
            0m,
            "test:zero-value-event",
            "{}",
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(
                    teacher.Id,
                    TeacherAllocationMode.CommissionRate,
                    0.20m,
                    0m,
                    0m,
                    0m,
                    student.FullName,
                    student.PhoneNumber,
                    "Free Lesson")
            }), CancellationToken.None);

        Assert.Single(db.TeacherFinancialEvents);
        Assert.Single(db.TeacherFinancialAllocations);
        Assert.Empty(db.TeacherAccounts);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task TeacherFinanceQueries_ReturnLedgerCalendarAndTransactions()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Calendar Teacher", "01030000005");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Calendar Student", "01030000006");
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            User = teacherUser,
            CommissionRate = 0.10m,
            Specialization = "Math",
            ContactInfo = "phone"
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();

        var occurredAt = DateTime.UtcNow.Date.AddHours(10);
        var service = new TeacherAccountingService(db);
        await service.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.AccessCodeActivation,
            Guid.NewGuid(),
            student.Id,
            SalesTargetType.Package,
            Guid.NewGuid(),
            200m,
            0m,
            200m,
            0m,
            180m,
            "test:calendar-event",
            "{}",
            occurredAt,
            TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(
                    teacher.Id,
                    TeacherAllocationMode.CommissionRate,
                    0.10m,
                    200m,
                    20m,
                    180m,
                    student.FullName,
                    student.PhoneNumber,
                    "Package",
                    1234)
            }), CancellationToken.None);

        var calendarHandler = new GetTeacherFinanceCalendarQueryHandler(db);
        var calendar = await calendarHandler.Handle(
            new GetTeacherFinanceCalendarQuery(teacherUser.Id, occurredAt.Date, occurredAt.Date),
            CancellationToken.None);

        Assert.True(calendar.Success);
        var day = Assert.Single(calendar.Data!);
        Assert.Equal(20m, day.TeacherShareAmount);
        Assert.Equal(1, day.TransactionCount);

        var transactionsHandler = new GetTeacherTransactionsQueryHandler(db);
        var transactions = await transactionsHandler.Handle(
            new GetTeacherTransactionsQuery(teacherUser.Id, occurredAt.Date, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.True(transactions.Success);
        var transaction = Assert.Single(transactions.Data!.Items);
        Assert.Equal("Package", transaction.ContentName);
        Assert.Equal("Calendar Student", transaction.StudentName);
        Assert.Equal(1234, transaction.CodeSerialNumber);
        Assert.Equal(20m, transaction.TeacherShareAmount);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task ReverseTargetAsync_WhenAllocationUnpaid_ReversesLedgerAndTeacherBalance()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Refund Teacher", "01030000007");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Refund Student", "01030000008");
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.40m,
            Specialization = "Math",
            ContactInfo = "phone"
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();

        var packageId = Guid.NewGuid();
        var service = new TeacherAccountingService(db);
        await service.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase,
            Guid.NewGuid(),
            student.Id,
            SalesTargetType.Package,
            packageId,
            100m,
            0m,
            100m,
            0m,
            60m,
            "test:unpaid-reversal",
            "{}",
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(
                    teacher.Id,
                    TeacherAllocationMode.CommissionRate,
                    0.40m,
                    100m,
                    40m,
                    60m,
                    student.FullName,
                    student.PhoneNumber,
                    "Package")
            }), CancellationToken.None);

        var reversedCount = await service.ReverseTargetAsync(
            student.Id,
            SalesTargetType.Package,
            packageId,
            Guid.NewGuid(),
            "Package grant cancelled",
            CancellationToken.None);

        Assert.Equal(1, reversedCount);
        var account = await db.TeacherAccounts.SingleAsync();
        Assert.Equal(0m, account.TotalEarnings);
        Assert.Equal(0m, account.CurrentBalance);

        var allocations = await db.TeacherFinancialAllocations
            .OrderBy(a => a.TeacherShareAmount)
            .ToListAsync();
        Assert.Equal(2, allocations.Count);
        Assert.Equal(-40m, allocations[0].TeacherShareAmount);
        Assert.Equal(TeacherAllocationMode.Reversal, allocations[0].AllocationMode);
        Assert.Equal(TeacherFinancialPayoutStatus.Reversed, allocations[0].PayoutStatus);
        Assert.Equal(TeacherFinancialPayoutStatus.Reversed, allocations[1].PayoutStatus);
        Assert.Empty(db.TeacherPayoutAdjustments);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task ReverseTargetAsync_WhenAllocationPaid_CreatesOpenDebtAdjustment()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Paid Refund Teacher", "01030000009");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Paid Refund Student", "01030000010");
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.30m,
            Specialization = "Physics",
            ContactInfo = "phone"
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();

        var lessonId = Guid.NewGuid();
        var service = new TeacherAccountingService(db);
        await service.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase,
            Guid.NewGuid(),
            student.Id,
            SalesTargetType.Lesson,
            lessonId,
            200m,
            0m,
            200m,
            0m,
            140m,
            "test:paid-reversal",
            "{}",
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(
                    teacher.Id,
                    TeacherAllocationMode.CommissionRate,
                    0.30m,
                    200m,
                    60m,
                    140m,
                    student.FullName,
                    student.PhoneNumber,
                    "Lesson")
            }), CancellationToken.None);

        var payout = new TeacherPayout
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            Amount = 60m,
            Status = PayoutStatus.Paid,
            PaidAt = DateTime.UtcNow
        };
        db.TeacherPayouts.Add(payout);
        var paidAllocation = await db.TeacherFinancialAllocations.SingleAsync();
        paidAllocation.PayoutId = payout.Id;
        paidAllocation.PayoutStatus = TeacherFinancialPayoutStatus.Paid;
        await db.SaveChangesAsync();

        var reversedCount = await service.ReverseTargetAsync(
            student.Id,
            SalesTargetType.Lesson,
            lessonId,
            Guid.NewGuid(),
            "Paid sale cancelled",
            CancellationToken.None);

        Assert.Equal(1, reversedCount);
        Assert.Equal(60m, (await db.TeacherAccounts.SingleAsync()).CurrentBalance);

        var originalAllocation = await db.TeacherFinancialAllocations
            .Where(a => a.TeacherShareAmount > 0m)
            .SingleAsync();
        Assert.Equal(TeacherFinancialPayoutStatus.Debt, originalAllocation.PayoutStatus);

        var adjustment = await db.TeacherPayoutAdjustments.SingleAsync();
        Assert.Equal(teacher.Id, adjustment.TeacherId);
        Assert.Equal(payout.Id, adjustment.RelatedPayoutId);
        Assert.Equal(-60m, adjustment.Amount);
        Assert.Equal(TeacherPayoutAdjustmentStatus.Open, adjustment.Status);
    }
}
