using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

public sealed class FinancialPostingServiceTests
{
    [Fact]
    public async Task PostAsync_creates_one_balanced_entry_and_retry_returns_same_entry()
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            Account("1000", FinancialAccountType.Asset, FinancialNormalSide.Debit, FinancialAccountRole.Treasury),
            Account("1100", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.GeneralStudentLiability));
        await db.SaveChangesAsync();
        var service = new FinancialPostingService(db);
        var request = new FinancialPostingRequest(
            "Recharge", Guid.NewGuid(), "RechargePost", "recharge:test:1", "Test recharge", DateTime.UtcNow, null,
            [new("1000", 100m, 0m), new("1100", 0m, 100m)]);

        var first = await service.PostAsync(request);
        var second = await service.PostAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(db.JournalEntries);
        Assert.Equal(100m, first.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, first.Lines.Sum(line => line.Credit));
    }

    [Fact]
    public async Task PostAsync_rejects_unbalanced_lines()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new FinancialPostingService(db);
        var request = new FinancialPostingRequest(
            "Test", null, "Test", "test:unbalanced", "Invalid", DateTime.UtcNow, null,
            [new("1000", 100m, 0m), new("1100", 0m, 99m)]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(request));
        Assert.Equal("FINANCE_UNBALANCED_ENTRY", exception.Message);
    }

    [Fact]
    public async Task Half_cent_rounding_cannot_persist_an_unbalanced_entry()
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            Account("1000", FinancialAccountType.Asset, FinancialNormalSide.Debit, FinancialAccountRole.Treasury),
            Account("1100", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.GeneralStudentLiability));
        await db.SaveChangesAsync();
        var request = new FinancialPostingRequest("Test", null, "Test", "audit:half-cent", "Rounding audit", DateTime.UtcNow, null,
            [new("1000", 1.005m, 0m), new("1000", 1.005m, 0m), new("1100", 0m, 2.005m)]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new FinancialPostingService(db).PostAsync(request));
        Assert.Equal("FINANCE_UNBALANCED_ENTRY", error.Message);
        Assert.Empty(db.JournalEntries);
    }

    [Fact]
    public async Task PostAsync_rejects_a_line_that_has_both_sides()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new FinancialPostingService(db);
        var request = new FinancialPostingRequest(
            "Test", null, "Test", "test:both-sides", "Invalid", DateTime.UtcNow, null,
            [new("1000", 1m, 1m), new("1100", 0m, 2m)]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(request));
        Assert.Equal("FINANCE_INVALID_LINE", exception.Message);
    }

    [Fact]
    public async Task PostAsync_rejects_a_closed_accounting_period()
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            Account("1000", FinancialAccountType.Asset, FinancialNormalSide.Debit, FinancialAccountRole.Treasury),
            Account("1100", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.GeneralStudentLiability));
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            StartDate = DateTime.UtcNow.Date.AddDays(-1),
            EndDate = DateTime.UtcNow.Date.AddDays(1),
            Status = AccountingPeriodStatus.Closed
        });
        await db.SaveChangesAsync();
        var service = new FinancialPostingService(db);
        var request = new FinancialPostingRequest(
            "Recharge", Guid.NewGuid(), "RechargePost", "recharge:closed", "Closed", DateTime.UtcNow, null,
            [new("1000", 100m, 0m), new("1100", 0m, 100m)]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(request));
        Assert.Equal("FINANCE_PERIOD_CLOSED", exception.Message);
    }

    [Fact]
    public async Task ReverseAsync_creates_opposite_lines_and_marks_original()
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            Account("1000", FinancialAccountType.Asset, FinancialNormalSide.Debit, FinancialAccountRole.Treasury),
            Account("1100", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.GeneralStudentLiability));
        await db.SaveChangesAsync();
        var service = new FinancialPostingService(db);
        var original = await service.PostAsync(new FinancialPostingRequest(
            "Recharge", Guid.NewGuid(), "RechargePost", "recharge:reverse", "Original", DateTime.UtcNow, null,
            [new("1000", 100m, 0m), new("1100", 0m, 100m)]));

        var reversal = await service.ReverseAsync(original.Id, null, "Correction");

        Assert.Equal(JournalEntryStatus.Reversed, original.Status);
        Assert.Equal(100m, reversal.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, reversal.Lines.Sum(line => line.Credit));
        Assert.Equal(100m, reversal.Lines.Single(line => line.FinancialAccount.Code == "1100").Debit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(100)]
    public async Task Historical_purchase_uses_recorded_general_and_teacher_funding_once(decimal scoped)
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            Account("1100", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.GeneralStudentLiability),
            Account("1110", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.TeacherStudentLiability),
            Account("2000", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.TeacherPayable),
            Account("4000", FinancialAccountType.Revenue, FinancialNormalSide.Credit, FinancialAccountRole.PlatformRevenue));
        var when = DateTime.UtcNow.AddDays(-1);
        db.SalesFinancialEffects.Add(new SalesFinancialEffect {
            PurchaseOperationId = Guid.NewGuid(), StudentId = Guid.NewGuid(), TeacherId = Guid.NewGuid(),
            CreatedAt = when, PaidAmount = 100m, TeacherShareImpact = 85m, PlatformShareImpact = 15m,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { paidTeacherBalanceAmount = scoped })
        });
        await db.SaveChangesAsync();
        var migration = new PlatformFinanceMigrationService(db, new FinancialPostingService(db));

        var first = await migration.PostAsync(when.Date, when.Date.AddDays(1), Guid.NewGuid(), default);
        var retry = await migration.PostAsync(when.Date, when.Date.AddDays(1), Guid.NewGuid(), default);

        Assert.Equal(0, first.Failed);
        Assert.Equal(1, first.Posted);
        Assert.Equal(1, retry.AlreadyPosted);
        var entry = Assert.Single(db.JournalEntries);
        Assert.Equal(scoped, entry.Lines.Where(x => x.FinancialAccount.Code == "1110").Sum(x => x.Debit));
        Assert.Equal(100m - scoped, entry.Lines.Where(x => x.FinancialAccount.Code == "1100").Sum(x => x.Debit));
        Assert.Equal(100m, entry.Lines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task Historical_settlement_records_net_cash_once_including_debt_deduction()
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            Account("1000", FinancialAccountType.Asset, FinancialNormalSide.Debit, FinancialAccountRole.Treasury),
            Account("2000", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.TeacherPayable));
        var paidAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        db.TeacherSettlements.Add(new TeacherSettlement { TeacherId = Guid.NewGuid(), Status = TeacherSettlementStatus.Paid,
            GrossDueAmount = 100, DebtDeductionAmount = 20, NetPayableAmount = 80, PaidAt = paidAt });
        await db.SaveChangesAsync();
        var migration = new PlatformFinanceMigrationService(db, new FinancialPostingService(db));
        var preview = await migration.PreviewAsync(paidAt.Date, paidAt.Date, default);
        Assert.Equal(80m, preview.TeacherPayoutAmount);
        var first = await migration.PostAsync(paidAt.Date, paidAt.Date, Guid.NewGuid(), default);
        var retry = await migration.PostAsync(paidAt.Date, paidAt.Date, Guid.NewGuid(), default);
        Assert.Equal(1, first.Posted);
        Assert.Equal(1, retry.AlreadyPosted);
        var journal = Assert.Single(db.JournalEntries);
        Assert.Equal("TeacherSettlement", journal.SourceType);
        Assert.Equal(80m, journal.Lines.Sum(line => line.Credit));
        Assert.Equal(80m, journal.Lines.Sum(line => line.Debit));
    }

    private static FinancialAccount Account(string code, FinancialAccountType type, FinancialNormalSide side, FinancialAccountRole role) => new()
    {
        Code = code, Name = code, Type = type, NormalSide = side, Role = role
    };
}
