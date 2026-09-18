using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Migrations;
using Npgsql;

namespace NaderGorge.Application.Tests;

public sealed class TeacherPaidFundingCorrectionTests
{
    private static readonly Guid TeacherId = Guid.Parse("2a0e7d2f-1dd7-4af0-9974-c999489899b2");
    private static readonly DateTime SaleTime = new(2026, 9, 17, 12, 0, 0);

    [FinanceRepairTheory]
    [InlineData(0, 300, 50)]
    [InlineData(100, 300, 150)]
    [InlineData(400, 300, 450)]
    public async Task Paid_scoped_recharge_corrects_earnings_once_and_remains_refundable(decimal general, decimal scoped, decimal expected)
    {
        await using var db = await OpenDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var sale = await SeedSale(db, general, scoped);
        var originalEvent = await db.TeacherFinancialEvents.AsNoTracking().SingleAsync(x => x.SourceId == sale.PurchaseOperationId);
        var originalJournal = await db.JournalEntries.AsNoTracking().SingleAsync(x => x.SourceId == sale.PurchaseOperationId);
        await Correct(db);
        await Correct(db);
        db.ChangeTracker.Clear();

        var account = await db.TeacherAccounts.SingleAsync(x => x.TeacherId == TeacherId);
        Assert.Equal(expected, account.TotalEarnings);
        Assert.Equal(expected, account.CurrentBalance);
        var unchanged = await db.TeacherFinancialEvents.AsNoTracking().SingleAsync(x => x.Id == originalEvent.Id);
        Assert.Equal(originalEvent.PaidAmount, unchanged.PaidAmount);
        Assert.Equal(originalEvent.DetailsJson, unchanged.DetailsJson);
        Assert.Equal(originalJournal.Status, (await db.JournalEntries.FindAsync(originalJournal.Id))!.Status);
        var correctedSale = await db.SalesFinancialEffects.SingleAsync(x => x.Id == sale.Id);
        Assert.Equal(general + scoped, correctedSale.PaidAmount);
        Assert.Equal(0, correctedSale.PromotionalAmount);
        Assert.Equal(expected, correctedSale.TeacherShareImpact);
        Assert.Equal(general + scoped - expected, correctedSale.PlatformShareImpact);
        Assert.Single(await db.TeacherFinancialEvents.Where(x => x.SourceId == sale.PurchaseOperationId && x.SourceType == TeacherFinancialSourceType.ManualAdjustment).ToListAsync());
        var lines = await db.JournalLines.Include(x => x.FinancialAccount).Where(x => x.JournalEntry.SourceId == sale.PurchaseOperationId).ToListAsync();
        Assert.Equal(lines.Sum(x => x.Debit), lines.Sum(x => x.Credit));
        Assert.Equal(expected, lines.Where(x => x.FinancialAccount.Code == "2000").Sum(x => x.Credit - x.Debit));
        Assert.Equal(scoped, lines.Where(x => x.FinancialAccount.Code == "1110").Sum(x => x.Debit - x.Credit));

        await new TeacherAccountingService(db).ReverseTargetAsync(sale.StudentId, sale.TargetType, sale.TargetId,
            Guid.NewGuid(), "Test refund", default, new TeacherRefundScope(sale.PurchaseOperationId, 1m));
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.TeacherAccounts.SingleAsync(x => x.TeacherId == TeacherId)).CurrentBalance);
        await transaction.RollbackAsync();
    }

    [FinanceRepairTheory]
    [InlineData("gift")]
    [InlineData("zero-agreement")]
    [InlineData("cancelled")]
    [InlineData("new-format")]
    [InlineData("journal-mismatch")]
    public async Task Unproven_or_ineligible_history_is_preserved(string scenario)
    {
        await using var db = await OpenDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var sale = await SeedSale(db, 0, 300, scenario);
        await Correct(db);
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.TeacherAccounts.SingleAsync(x => x.TeacherId == TeacherId)).CurrentBalance);
        Assert.Single(await db.TeacherFinancialEvents.Where(x => x.SourceId == sale.PurchaseOperationId).ToListAsync());
        Assert.Equal(0, (await db.SalesFinancialEffects.SingleAsync(x => x.Id == sale.Id)).PaidAmount);
        await transaction.RollbackAsync();
    }

    [FinanceRepairFact]
    public async Task Closed_period_blocks_the_whole_correction()
    {
        await using var db = await OpenDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await SeedSale(db, 0, 300);
        db.AccountingPeriods.Add(new AccountingPeriod { StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date,
            Status = AccountingPeriodStatus.Closed });
        await db.SaveChangesAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => Correct(db));
        Assert.Contains("open accounting period", exception.MessageText);
        await transaction.RollbackAsync();
    }

    [FinanceRepairFact]
    public async Task Profit_report_separates_paid_recharge_from_profit_and_reconciles_current_teacher_accounts()
    {
        await using var db = await OpenDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await SeedSale(db, 100, 300);
        var codes = new Dictionary<string, FinancialAccountRole> { ["1100"] = FinancialAccountRole.GeneralStudentLiability,
            ["1110"] = FinancialAccountRole.TeacherStudentLiability, ["2000"] = FinancialAccountRole.TeacherPayable,
            ["4000"] = FinancialAccountRole.PlatformRevenue };
        foreach (var account in await db.FinancialAccounts.ToListAsync())
        {
            account.Role = codes.GetValueOrDefault(account.Code);
            account.Type = account.Code == "4000" ? FinancialAccountType.Revenue : FinancialAccountType.Liability;
        }
        db.TeacherProfiles.Add(new TeacherProfile { User = new User { FullName = "No sales", PhoneNumber = "01000000002", PasswordHash = "!" } });
        await db.SaveChangesAsync();
        var query = new NaderGorge.Application.Features.Admin.PlatformFinance.PlatformProfitReportQuery(db,
            new NaderGorge.Application.Features.Admin.PlatformFinance.PlatformFinanceDashboardService(db),
            new NaderGorge.Application.Features.Admin.PlatformFinance.Teachers.GetTeacherFinancialSummaryQuery(db));
        var before = await query.GetAsync(SaleTime.Date, DateTime.UtcNow.Date.AddDays(1), default);
        Assert.Equal(300, before.Teachers.Single(x => x.Period.TeacherId == TeacherId).ReconciliationDifference);
        await Correct(db);
        db.ChangeTracker.Clear();
        var report = await query.GetAsync(SaleTime.Date, DateTime.UtcNow.Date.AddDays(1), default);
        var teacher = report.Teachers.Single(x => x.Period.TeacherId == TeacherId);
        Assert.Equal(400, teacher.Period.GrossSales);
        Assert.Equal(150, teacher.Period.TeacherShare);
        Assert.Equal(250, teacher.Period.PlatformShare);
        Assert.Equal(0, teacher.ReconciliationDifference);
        Assert.Equal(250, report.Platform.NetProfit);
        Assert.Equal(0, report.Teachers.Single(x => x.Period.TeacherName == "No sales").Period.GrossSales);
        await transaction.RollbackAsync();
    }

    private static async Task<AppDbContext> OpenDatabase()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var connection = Environment.GetEnvironmentVariable("FINANCE_REPAIR_TEST_DB")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("finance_repair_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        return db;
    }

    private static Task Correct(AppDbContext db) => db.Database.ExecuteSqlRawAsync(
        Assert.Single(new CorrectTeacherPaidFunding().UpOperations.OfType<SqlOperation>()).Sql);

    private static async Task<SalesFinancialEffect> SeedSale(AppDbContext db, decimal general, decimal scoped, string scenario = "eligible")
    {
        var student = new User { FullName = "Finance repair test", PhoneNumber = "01000000001", PasswordHash = "!", IsActive = false };
        var teacher = new TeacherProfile { Id = TeacherId, User = student };
        var package = new Package { Name = "Finance test", Description = "Test", Subject = new Subject { Name = "Test" },
            Teacher = teacher, TargetGrade = "3", Price = general + scoped };
        var agreement = new TeacherFinancialAgreement { Teacher = teacher, CreatedByUserId = student.Id,
            ScopeType = TeacherAgreementScopeType.Package, ScopeId = package.Id, AllocationMode = TeacherAgreementAllocationMode.PlatformFixedPerUnit,
            AllocationValue = 250, PriceBasis = TeacherPriceBasis.NetAfterDiscount, EffectiveFrom = SaleTime.AddDays(-1), Reason = "Test" };
        var oldShare = Math.Max(0, general - 250);
        var account = new TeacherAccount { Teacher = teacher, TotalEarnings = oldShare, CurrentBalance = oldShare };
        var funding = Guid.NewGuid();
        var purchase = Guid.NewGuid();
        var original = new TeacherFinancialEvent { SourceType = TeacherFinancialSourceType.DirectPurchase, SourceId = purchase,
            Student = student, TargetType = SalesTargetType.Package, TargetId = package.Id, GrossAmount = general + scoped,
            PaidAmount = general, PromotionalAmount = scoped, PlatformShareAmount = general - oldShare, OccurredAt = SaleTime,
            IdempotencyKey = $"purchase:{purchase}", DetailsJson = JsonSerializer.Serialize(new { fundingOperationId = funding }) };
        original.Allocations.Add(new TeacherFinancialAllocation { Teacher = teacher, AllocationMode = TeacherAllocationMode.FixedAmount,
            AllocationValue = 250, GrossBasisAmount = general, TeacherShareAmount = oldShare, PlatformShareAmount = general - oldShare,
            AgreementId = agreement.Id, AgreementAllocationMode = TeacherAgreementAllocationMode.PlatformFixedPerUnit,
            PriceBasis = TeacherPriceBasis.NetAfterDiscount, ContentNameSnapshot = "Test" });
        var issuance = new GiftIssuance { RequestId = Guid.NewGuid(), TargetType = GiftTargetType.TeacherBalance, Amount = scoped,
            Teacher = teacher, IssuedByUser = student, Reason = "Test" };
        var recipient = new GiftRecipient { GiftIssuance = issuance, Student = student, OutcomeCode = scenario == "gift" ? "GRANTED" : "DIGITAL_RECHARGE" };
        var allocation = new PromotionalBalanceAllocation { GiftRecipient = recipient, Student = student, Teacher = teacher,
            OriginalAmount = scoped, ConsumedAmount = scoped };
        allocation.Usages.Add(new PromotionalBalanceUsage { GiftRecipient = recipient, PurchaseOperationId = funding,
            ContentType = CodeType.Package, ContentId = package.Id, Amount = scoped });
        var grant = new StudentAccessGrant { User = student, PackageId = package.Id, GrantType = CodeType.Package,
            GrantedAt = SaleTime, IsActive = scenario != "cancelled", CancelledAt = scenario == "cancelled" ? SaleTime.AddMinutes(1) : null };
        var sale = new SalesFinancialEffect { PurchaseOperationId = purchase, Student = student, Teacher = teacher,
            TargetType = SalesTargetType.Package, TargetId = package.Id, GrossAmount = general + scoped,
            PaidAmount = general, PromotionalAmount = scoped, TeacherShareImpact = oldShare, PlatformShareImpact = general - oldShare };
        if (scenario == "zero-agreement") original.Allocations.Single().AgreementAllocationMode = TeacherAgreementAllocationMode.FixedPerSale;
        if (scenario == "new-format") original.DetailsJson = JsonSerializer.Serialize(new { fundingOperationId = funding, paidTeacherBalanceAmount = scoped });
        db.AddRange(package, agreement, account, original, allocation, grant, sale);
        var journal = new JournalEntry { SequenceNumber = (await db.JournalEntries.MaxAsync(x => (long?)x.SequenceNumber) ?? 0) + 1,
            SourceType = "Purchase", SourceId = purchase, IdempotencyKey = $"purchase:{purchase:N}", PostingKind = "PurchaseRecognized", OccurredAt = SaleTime };
        foreach (var (code, debit, credit) in new[] { ("1100", general, 0m), ("1110", scoped, 0m), ("2000", 0m, oldShare + scoped), ("4000", 0m, general - oldShare) })
        {
            if (debit + credit == 0) continue;
            var financialAccount = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.Code == code) ?? new FinancialAccount { Code = code, Name = code };
            journal.Lines.Add(new JournalLine { FinancialAccount = financialAccount, Debit = debit, Credit = credit,
                StudentId = student.Id, TeacherId = code is "1110" or "2000" ? TeacherId : null });
        }
        if (scenario == "journal-mismatch") journal.Lines.First().Debit += 1;
        db.JournalEntries.Add(journal);
        foreach (var code in new[] { "2000", "4000" })
            if (!db.FinancialAccounts.Local.Any(x => x.Code == code) && !await db.FinancialAccounts.AnyAsync(x => x.Code == code))
                db.FinancialAccounts.Add(new FinancialAccount { Code = code, Name = code });
        await db.SaveChangesAsync();
        return sale;
    }
}

public sealed class FinanceRepairTheoryAttribute : TheoryAttribute
{
    public FinanceRepairTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FINANCE_REPAIR_TEST_DB")))
            Skip = "Requires a migrated local finance_repair_test PostgreSQL database.";
    }
}

public sealed class FinanceRepairFactAttribute : FactAttribute
{
    public FinanceRepairFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FINANCE_REPAIR_TEST_DB")))
            Skip = "Requires a migrated local finance_repair_test PostgreSQL database.";
    }
}
