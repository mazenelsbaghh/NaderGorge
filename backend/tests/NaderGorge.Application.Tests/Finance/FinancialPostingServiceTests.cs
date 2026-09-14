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

    private static FinancialAccount Account(string code, FinancialAccountType type, FinancialNormalSide side, FinancialAccountRole role) => new()
    {
        Code = code, Name = code, Type = type, NormalSide = side, Role = role
    };
}
