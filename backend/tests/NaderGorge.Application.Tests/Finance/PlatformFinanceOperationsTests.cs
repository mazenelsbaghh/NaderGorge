using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace NaderGorge.Application.Tests.Finance;

public sealed class PlatformFinanceOperationsTests
{
    [Fact]
    public async Task Paid_expense_posts_to_operating_expense_and_treasury()
    {
        await using var db = TestAppDbContextFactory.Create();
        var account = new FinancialAccount { Code = "5000", Name = "Expense", Type = FinancialAccountType.Expense, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.OperatingExpense };
        var cash = new FinancialAccount { Code = "1000", Name = "Cash", Type = FinancialAccountType.Asset, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury };
        var category = new ExpenseCategory { Name = "Operations", AccountCode = "5000" };
        var treasury = new TreasuryAccount { Name = "Cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = cash.Id };
        db.AddRange(account, cash, category, treasury);
        await db.SaveChangesAsync();
        var operations = new PlatformFinanceOperationsService(db, new FinancialPostingService(db), new BalanceService(db, NullLogger<BalanceService>.Instance));

        var expense = await operations.CreateExpenseAsync(new CreatePlatformExpenseRequest(250m, DateTime.UtcNow, category.Id, null, null, "Internet", null, Guid.NewGuid()), CancellationToken.None);
        var posted = await operations.PostExpenseAsync(expense.Id, new PostPlatformExpenseRequest(treasury.Id, Guid.NewGuid(), "expense-post-1", null), CancellationToken.None);

        Assert.Equal(PlatformExpenseStatus.Paid, posted.Status);
        var journal = Assert.Single(db.JournalEntries);
        Assert.Equal(250m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(250m, journal.Lines.Sum(line => line.Credit));
    }

    [Theory]
    [InlineData(80, 20)]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    public async Task Cash_refund_requires_treasury_and_posts_platform_and_teacher_lines(decimal platformAmount, decimal teacherAmount)
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            new FinancialAccount { Code = "1000", Name = "Cash", Type = FinancialAccountType.Asset, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury },
            new FinancialAccount { Code = "2000", Name = "Teacher payable", Type = FinancialAccountType.Liability, NormalSide = FinancialNormalSide.Credit, Role = FinancialAccountRole.TeacherPayable },
            new FinancialAccount { Code = "4100", Name = "Refunds", Type = FinancialAccountType.ContraRevenue, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Refunds });
        var cash = db.FinancialAccounts.Local.Single(x => x.Code == "1000");
        var treasury = new TreasuryAccount { Name = "Cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = cash.Id };
        db.TreasuryAccounts.Add(treasury);
        var purchaseId = Guid.NewGuid();
        db.SalesFinancialEffects.Add(new SalesFinancialEffect
        {
            PurchaseOperationId = purchaseId,
            StudentId = Guid.NewGuid(),
            TargetType = SalesTargetType.Package,
            TargetId = Guid.NewGuid(),
            PaidAmount = 100m,
            GrossAmount = 100m,
            PlatformShareImpact = 80m,
            TeacherShareImpact = 20m
        });
        await db.SaveChangesAsync();
        var operations = new PlatformFinanceOperationsService(db, new FinancialPostingService(db), new BalanceService(db, NullLogger<BalanceService>.Instance));
        var refund = await operations.CreateRefundAsync(new CreatePlatformRefundRequest(purchaseId, "Purchase", Guid.NewGuid(), Guid.NewGuid(), platformAmount, teacherAmount, 2, treasury.Id, "Student request", "REF-1", Guid.NewGuid()), CancellationToken.None);

        await operations.PostRefundAsync(refund.Id, "refund-post-1", Guid.NewGuid(), CancellationToken.None);

        var journal = Assert.Single(db.JournalEntries);
        Assert.Equal(100m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, journal.Lines.Sum(line => line.Credit));
        Assert.Contains(journal.Lines, line => line.FinancialAccountId == cash.Id && line.Credit == 100m);
    }

    [Fact]
    public async Task Access_code_grant_refund_cannot_exceed_its_explicit_content_price_ceiling()
    {
        await using var db = TestAppDbContextFactory.Create();
        var operations = new PlatformFinanceOperationsService(
            db,
            new FinancialPostingService(db),
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var grantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var package = new Package
        {
            Name = "Historical package",
            Description = "Production refund regression",
            Price = 1350m,
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            TargetGrade = "SecondSecondary"
        };
        db.AddRange(package, new StudentAccessGrant
        {
            Id = grantId,
            UserId = studentId,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            AccessCodeId = Guid.NewGuid(),
            IsActive = false
        });
        await db.SaveChangesAsync();

        var refund = await operations.CreateRefundAsync(new CreatePlatformRefundRequest(
            grantId, "HistoricalAccessGrant", studentId, null, 1300m, 0m, 1, null,
            "Production refund regression", null, Guid.NewGuid(), grantId), CancellationToken.None);

        Assert.Equal(1300m, refund.TotalAmount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.CreateRefundAsync(
            new CreatePlatformRefundRequest(grantId, "HistoricalAccessGrant", studentId, null, 51m, 0m, 1, null,
                "Exceeds the remaining ceiling", null, Guid.NewGuid(), grantId), CancellationToken.None));
    }

    [Fact]
    public async Task Zero_cash_purchase_refund_cannot_exceed_original_gross_amount()
    {
        await using var db = TestAppDbContextFactory.Create();
        var operations = new PlatformFinanceOperationsService(
            db,
            new FinancialPostingService(db),
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var purchaseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        db.SalesFinancialEffects.Add(new SalesFinancialEffect
        {
            PurchaseOperationId = purchaseId,
            StudentId = studentId,
            TargetType = SalesTargetType.Lesson,
            TargetId = Guid.NewGuid(),
            GrossAmount = 50m,
            PromotionalAmount = 50m,
            PaidAmount = 0m
        });
        await db.SaveChangesAsync();

        var refund = await operations.CreateRefundAsync(new CreatePlatformRefundRequest(
            purchaseId, "PurchaseOperation", studentId, null, 40m, 0m, 1, null,
            "Zero cash purchase regression", null, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(40m, refund.TotalAmount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.CreateRefundAsync(
            new CreatePlatformRefundRequest(purchaseId, "PurchaseOperation", studentId, null, 11m, 0m, 1, null,
                "Exceeds original gross amount", null, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Zero_value_purchase_refund_cannot_exceed_nearest_positive_parent_price()
    {
        await using var db = TestAppDbContextFactory.Create();
        var operations = new PlatformFinanceOperationsService(
            db,
            new FinancialPostingService(db),
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var purchaseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var section = new ContentSection
        {
            Title = "Paid month",
            Price = 220m,
            TermId = Guid.NewGuid()
        };
        var lesson = new Lesson
        {
            Title = "Free lesson",
            Price = 0m,
            ContentSectionId = section.Id
        };
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Lesson,
            ContentSectionId = section.Id,
            LessonId = lesson.Id,
            IsActive = true
        };
        db.AddRange(section, lesson, grant, new SalesFinancialEffect
        {
            PurchaseOperationId = purchaseId,
            StudentId = studentId,
            TargetType = SalesTargetType.Lesson,
            TargetId = lesson.Id,
            GrossAmount = 0m,
            PaidAmount = 0m
        });
        await db.SaveChangesAsync();

        var refund = await operations.CreateRefundAsync(new CreatePlatformRefundRequest(
            purchaseId, "PurchaseOperation", studentId, null, 200m, 0m, 1, null,
            "Zero value purchase regression", null, Guid.NewGuid(), grant.Id), CancellationToken.None);

        Assert.Equal(200m, refund.TotalAmount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.CreateRefundAsync(
            new CreatePlatformRefundRequest(purchaseId, "PurchaseOperation", studentId, null, 21m, 0m, 1, null,
                "Exceeds parent price", null, Guid.NewGuid(), grant.Id), CancellationToken.None));
    }
}
