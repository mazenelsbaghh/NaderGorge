using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Application.Features.Admin.PlatformFinance.Reports;
using NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

// Audit reproductions, 2026-09-24. Assertions express the expected financial result.
public sealed class ProfitAuditReproductionTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly DateTime saleTime = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly User student = new() { FullName = "Audit student", PhoneNumber = "01000000001", PasswordHash = "test" };
    private readonly TeacherProfile teacher = new() { User = new User { FullName = "Audit teacher", PhoneNumber = "01000000002", PasswordHash = "test" } };

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.AddRange(student, teacher);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Cash_balance_includes_money_received_before_selected_period()
    {
        var cash = Account("1000", FinancialAccountRole.Treasury, FinancialAccountType.Asset);
        var capital = Account("3000", FinancialAccountRole.None, FinancialAccountType.Equity);
        db.Add(Journal("Opening", saleTime.AddDays(-10), cash, capital, 1000));
        await db.SaveChangesAsync();
        var dashboard = await new PlatformFinanceDashboardService(db).GetDashboardAsync(saleTime.Date, saleTime.Date, default);
        Assert.Equal(1000m, dashboard.Cash);
    }

    [Fact]
    public async Task Profit_refund_respects_explicit_platform_only_refund()
    {
        var sale = Sale(100, 60);
        db.Add(sale);
        var journal = new JournalEntry { SourceType = "PlatformRefund", Status = JournalEntryStatus.Posted,
            OccurredAt = saleTime.AddHours(1), IdempotencyKey = Guid.NewGuid().ToString(), PostingKind = "RefundPost", Description = "Audit refund" };
        db.Add(journal);
        db.Add(new PlatformRefund { OriginalSourceId = sale.SourceId, OriginalSourceType = "PurchaseOperation",
            StudentId = student.Id, TeacherId = teacher.Id, PlatformAmount = 20, TeacherAmount = 0,
            Status = PlatformRefundStatus.Posted, JournalEntryId = journal.Id, CreatedByUserId = student.Id, Reason = "Platform bears refund" });
        await db.SaveChangesAsync();
        var movements = await new ProfitSalesHistory(db).ReadAsync(saleTime.AddDays(1), default);
        Assert.Equal(60m, movements.Sum(x => x.TeacherShare));
        Assert.Equal(20m, movements.Sum(x => x.PlatformShare));
    }

    [Fact]
    public async Task Historical_lesson_sale_inherits_specific_package_agreement()
    {
        var package = new Package { Name = "Audit package", Description = "Audit", TargetGrade = "3", Teacher = teacher,
            Subject = new Subject { Name = "Audit subject" } };
        var term = new Term { Title = "Term", Package = package };
        var section = new ContentSection { Title = "Section", Term = term };
        var lesson = new Lesson { Title = "Lesson", ContentSection = section };
        db.Add(lesson);
        var sale = Sale(100, 60);
        sale.OccurredAt = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        sale.TargetType = SalesTargetType.Lesson;
        sale.TargetId = lesson.Id;
        db.Add(sale);
        foreach (var (scope, id, amount) in new[] {
            (TeacherAgreementScopeType.Default, (Guid?)null, 50m),
            (TeacherAgreementScopeType.Package, (Guid?)package.Id, 80m) })
            db.Add(new TeacherFinancialAgreement { Teacher = teacher, ScopeType = scope, ScopeId = id,
                Trigger = TeacherAgreementTrigger.ContentSale, AllocationMode = TeacherAgreementAllocationMode.Percentage,
                AllocationValue = amount, EffectiveFrom = sale.OccurredAt, CreatedByUserId = student.Id, Reason = "Audit terms" });
        await db.SaveChangesAsync();
        var movement = Assert.Single(await new ProfitSalesHistory(db).ReadAsync(saleTime, default));
        Assert.Equal(80m, movement.TeacherShare);
    }

    [Fact]
    public async Task Reports_and_dashboard_use_same_Cairo_business_day()
    {
        var cash = Account("1000", FinancialAccountRole.Treasury, FinancialAccountType.Asset);
        var revenue = Account("4000", FinancialAccountRole.PlatformRevenue, FinancialAccountType.Revenue);
        db.Add(Journal("Purchase", new DateTime(2026, 9, 19, 22, 30, 0, DateTimeKind.Utc), cash, revenue, 100));
        await db.SaveChangesAsync();
        var dashboard = await new PlatformFinanceDashboardService(db).GetDashboardAsync(saleTime.Date, saleTime.Date, default);
        var report = await new PlatformFinancialReportQueries(db).GetAsync("profit-loss", saleTime.Date, saleTime.Date, default);
        Assert.Equal(100m, dashboard.Revenue);
        Assert.Equal(dashboard.Revenue, report.Rows.Sum(x => x.Balance));
    }

    [Fact]
    public async Task Shared_sale_preserves_paid_total_when_splitting_cents()
    {
        var sale = Sale(100, 0);
        sale.SourceType = TeacherFinancialSourceType.SharedPackagePurchase;
        sale.Allocations.First().GrossBasisAmount = 1;
        for (var index = 0; index < 2; index++)
        {
            var other = new TeacherProfile { User = new User { FullName = $"Other {index}", PhoneNumber = $"0100000000{index + 3}", PasswordHash = "test" } };
            sale.Allocations.Add(new TeacherFinancialAllocation { Teacher = other, GrossBasisAmount = 1 });
        }
        db.Add(sale);
        await db.SaveChangesAsync();
        var movements = await new ProfitSalesHistory(db).ReadAsync(saleTime.AddDays(1), default);
        Assert.Equal(100m, movements.Sum(x => x.Sales));
    }

    [Fact]
    public async Task Reversed_sale_keeps_original_history_and_cancels_at_reversal_date()
    {
        var cash = Account("1000", FinancialAccountRole.Treasury, FinancialAccountType.Asset);
        var revenue = Account("4000", FinancialAccountRole.PlatformRevenue, FinancialAccountType.Revenue);
        var original = Journal("Purchase", saleTime, cash, revenue, 100);
        db.Add(original);
        await db.SaveChangesAsync();
        var reversal = await new FinancialPostingService(db).ReverseAsync(original.Id, student.Id, "Audit reversal");
        // Pin the simulated reversal so this regression does not depend on the machine clock.
        reversal.OccurredAt = saleTime.AddDays(1);
        await db.SaveChangesAsync();
        var dashboard = new PlatformFinanceDashboardService(db);
        var before = await dashboard.GetDashboardAsync(saleTime.Date, saleTime.Date, default);
        var after = await dashboard.GetDashboardAsync(saleTime.Date, saleTime.AddDays(1).Date, default);
        Assert.Equal(100m, before.Revenue);
        Assert.Equal(0m, after.Revenue);
        Assert.Equal(0m, after.Cash);
        Assert.Equal(2, (await dashboard.GetLedgerAsync(saleTime.Date, saleTime.AddDays(1).Date, 1, 20, default)).Count);
    }

    [Fact]
    public async Task Profit_and_income_statement_match_ledger_while_historical_difference_is_explicit()
    {
        var cash = Account("1000", FinancialAccountRole.Treasury, FinancialAccountType.Asset);
        var revenue = Account("4000", FinancialAccountRole.PlatformRevenue, FinancialAccountType.Revenue);
        var payable = Account("2000", FinancialAccountRole.TeacherPayable, FinancialAccountType.Liability);
        var entry = Journal("Purchase", saleTime, cash, revenue, 100);
        entry.Lines.Single(line => line.Credit > 0).Credit = 30;
        foreach (var line in entry.Lines) line.TeacherId = teacher.Id;
        entry.Lines.Add(new JournalLine { FinancialAccount = payable, Credit = 70, TeacherId = teacher.Id });
        db.AddRange(entry, Sale(100, 60), new TeacherAccount { Teacher = teacher, CurrentBalance = 70 });
        await db.SaveChangesAsync();
        var dashboard = new PlatformFinanceDashboardService(db);
        var summary = new GetTeacherFinancialSummaryQuery(db);
        var profit = await new PlatformProfitReportQuery(db, dashboard, summary).GetAsync(saleTime.Date, saleTime.Date, default);
        var statement = await new PlatformFinancialReportQueries(db).GetAsync("profit-loss", saleTime.Date, saleTime.Date, default);
        Assert.Equal(30m, profit.Platform.NetProfit);
        Assert.Equal(statement.Rows.Sum(row => row.Balance), profit.Platform.NetProfit);
        Assert.Equal(40m, profit.HistoricalPlatformNetRevenue);
        var row = Assert.Single(profit.Teachers);
        Assert.Equal(await summary.GetAsync(teacher.Id, saleTime.Date, saleTime.Date, default), row.Period);
        Assert.Equal(70m, row.Period.TeacherShare);
        Assert.Equal(60m, row.HistoricalPeriod.TeacherShare);
    }

    [Fact]
    public async Task Opening_teacher_balance_is_not_new_sales()
    {
        var cash = Account("1000", FinancialAccountRole.Treasury, FinancialAccountType.Asset);
        var payable = Account("2000", FinancialAccountRole.TeacherPayable, FinancialAccountType.Liability);
        var entry = Journal("Opening", saleTime, cash, payable, 500);
        foreach (var line in entry.Lines) line.TeacherId = teacher.Id;
        db.Add(entry);
        await db.SaveChangesAsync();
        var summary = await new GetTeacherFinancialSummaryQuery(db).GetAsync(teacher.Id, saleTime.Date, saleTime.Date, default);
        Assert.NotNull(summary);
        Assert.Equal(0m, summary.GrossSales);
        Assert.Equal(0m, summary.TeacherShare);
        Assert.Equal(500m, summary.Outstanding);
        Assert.Equal(500m, summary.Adjustments);
    }

    [Fact]
    public async Task Profit_report_compares_teacher_net_balance_after_open_debt_with_the_ledger()
    {
        var cash = Account("1000", FinancialAccountRole.Treasury, FinancialAccountType.Asset);
        var payable = Account("2000", FinancialAccountRole.TeacherPayable, FinancialAccountType.Liability);
        var entry = Journal("Opening", saleTime, cash, payable, 80m);
        foreach (var line in entry.Lines) line.TeacherId = teacher.Id;
        db.AddRange(entry, new TeacherAccount { Teacher = teacher, CurrentBalance = 100m, TotalEarnings = 100m },
            new TeacherPayoutAdjustment { Teacher = teacher, Amount = -20m });
        await db.SaveChangesAsync();
        var report = await new PlatformProfitReportQuery(db, new(db), new(db)).GetAsync(saleTime.Date, saleTime.Date, default);
        var row = Assert.Single(report.Teachers);
        Assert.Equal(80m, row.CurrentAccountBalance);
        Assert.Equal(0m, row.ReconciliationDifference);
        Assert.Equal(80m, row.Account!.NetPayable);
    }

    [Fact]
    public async Task Reversed_refund_does_not_erase_the_refund_from_an_earlier_period()
    {
        var sale = Sale(100, 60);
        var original = new JournalEntry { SourceType = "PlatformRefund", Status = JournalEntryStatus.Reversed,
            OccurredAt = saleTime.AddHours(1), IdempotencyKey = "refund-original", PostingKind = "RefundPost", Description = "Audit refund" };
        var reversal = new JournalEntry { SequenceNumber = 1, SourceType = "FinancialJournal", SourceId = original.Id,
            ReversalOfId = original.Id, OccurredAt = saleTime.AddDays(1), IdempotencyKey = "refund-reversal",
            PostingKind = "Reversal", Description = "Audit reversal" };
        db.AddRange(sale, original, reversal, new PlatformRefund { OriginalSourceId = sale.SourceId,
            OriginalSourceType = "PurchaseOperation", StudentId = student.Id, TeacherId = teacher.Id,
            PlatformAmount = 20, TeacherAmount = 0, Status = PlatformRefundStatus.Reversed,
            JournalEntryId = original.Id, CreatedByUserId = student.Id, Reason = "Refund later reversed" });
        await db.SaveChangesAsync();
        var history = new ProfitSalesHistory(db);
        var earlier = await history.ReadAsync(saleTime.AddHours(2), default);
        var after = await history.ReadAsync(saleTime.AddDays(2), default);
        Assert.Equal(20m, earlier.Sum(movement => movement.Refunds));
        Assert.Equal(20m, earlier.Sum(movement => movement.PlatformShare));
        Assert.Equal(0m, after.Sum(movement => movement.Refunds));
        Assert.Equal(40m, after.Sum(movement => movement.PlatformShare));
    }

    [Fact]
    public async Task Legacy_cancellation_after_partial_refund_only_removes_remaining_teacher_share()
    {
        var sale = Sale(100, 60);
        var package = new Package { Id = sale.TargetId, Name = "Audit package", Description = "Audit",
            TargetGrade = "3", Teacher = teacher, Subject = new Subject { Name = "Audit subject" } };
        var grant = new StudentAccessGrant { User = student, PackageId = package.Id, GrantType = CodeType.Package };
        var journal = new JournalEntry { SourceType = "PlatformRefund", OccurredAt = saleTime.AddHours(1),
            IdempotencyKey = "partial-refund", PostingKind = "RefundPost", Description = "Partial refund" };
        db.AddRange(package, sale, grant, journal, new PlatformRefund { OriginalSourceId = sale.SourceId,
            OriginalSourceType = "PurchaseOperation", StudentId = student.Id, TeacherId = teacher.Id,
            TeacherAmount = 20, Status = PlatformRefundStatus.Posted, JournalEntryId = journal.Id,
            CreatedByUserId = student.Id, Reason = "Partial refund" },
            new AuditLog { EntityId = grant.Id, EntityType = "StudentAccessGrant", Action = "CANCEL_PACKAGE_GRANT",
                PerformedByUserId = student.Id, CreatedAt = saleTime.AddHours(2), NewValues = "{\"refundedAmount\":0}" });
        await db.SaveChangesAsync();
        var movements = await new ProfitSalesHistory(db).ReadAsync(saleTime.AddDays(1), default);
        Assert.Equal(0m, movements.Sum(movement => movement.TeacherShare));
        Assert.Equal(80m, movements.Sum(movement => movement.PlatformShare));
        Assert.Equal(20m, movements.Sum(movement => movement.Refunds));
    }

    private TeacherFinancialEvent Sale(decimal paid, decimal teacherShare) => new()
    {
        SourceType = TeacherFinancialSourceType.DirectPurchase, SourceId = Guid.NewGuid(), Student = student,
        TargetType = SalesTargetType.Package, TargetId = Guid.NewGuid(), PaidAmount = paid, GrossAmount = paid,
        OccurredAt = saleTime, IdempotencyKey = Guid.NewGuid().ToString(), DetailsJson = "{}",
        Allocations = [new TeacherFinancialAllocation { Teacher = teacher, GrossBasisAmount = paid, TeacherShareAmount = teacherShare }]
    };

    private FinancialAccount Account(string code, FinancialAccountRole role, FinancialAccountType type)
    {
        var account = new FinancialAccount { Code = code, Name = code, Role = role, Type = type };
        db.Add(account);
        return account;
    }

    private static JournalEntry Journal(string source, DateTime at, FinancialAccount debit, FinancialAccount credit, decimal amount) => new()
    {
        SourceType = source, OccurredAt = at, Status = JournalEntryStatus.Posted, IdempotencyKey = Guid.NewGuid().ToString(),
        Description = "Audit entry", PostingKind = source,
        Lines = [new JournalLine { FinancialAccount = debit, Debit = amount }, new JournalLine { FinancialAccount = credit, Credit = amount }]
    };
}
