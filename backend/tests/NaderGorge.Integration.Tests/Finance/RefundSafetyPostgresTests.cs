using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class RefundSafetyPostgresTests
{
    [Theory]
    [InlineData(0, 490, 0)]
    [InlineData(100, 200, 190)]
    public async Task Legacy_paid_recharge_can_be_refunded_from_the_external_screen_once(int general, int recharge, int gift)
    {
        await using var scenario = await RefundTestScenario.CreateAsync();
        await SeedLegacyFunding(scenario, general, recharge, gift);
        var request = scenario.CashRequest(290m);
        var profile = await new NaderGorge.Application.Features.Admin.Queries.GetStudentProfileDetailQueryHandler(scenario.Db)
            .Handle(new(scenario.Student.Id), default);
        Assert.Equal(general + recharge, Assert.Single(profile.Packages).PaidAmount);

        var first = await scenario.Controller().CreateExternalPackageRefund(request, default);
        var replay = await scenario.Controller().CreateExternalPackageRefund(request, default);

        Assert.IsType<OkObjectResult>(first.Result);
        Assert.IsType<OkObjectResult>(replay.Result);
        await using var verify = scenario.CreateDbContext();
        var refund = await verify.PlatformRefunds.SingleAsync();
        Assert.Equal(290m, refund.TotalAmount);
        Assert.Equal(decimal.Round(120m * 290m / (general + recharge), 2), refund.TeacherAmount);
        Assert.Equal(PlatformRefundStatus.Posted, refund.Status);
        Assert.False((await verify.StudentAccessGrants.SingleAsync()).IsActive);
        Assert.Equal(20m, (await verify.StudentBalances.SingleAsync()).CurrentBalance);
        Assert.Empty(await verify.BalanceTransactions.ToArrayAsync());
        var journal = await verify.JournalEntries.Include(entry => entry.Lines).SingleAsync();
        Assert.Equal(290m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(290m, journal.Lines.Sum(line => line.Credit));
        Assert.Equal(general, (await verify.SalesFinancialEffects.SingleAsync()).PaidAmount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_mixed_funding_rejects_excess_cash_or_a_teacher_share_above_proven_cash(bool invalidTeacherShare)
    {
        await using var scenario = await RefundTestScenario.CreateAsync();
        await SeedLegacyFunding(scenario, 100, 200, 190);
        if (invalidTeacherShare)
        {
            var source = await scenario.Db.SalesFinancialEffects.SingleAsync();
            source.TeacherShareImpact = 400m;
            source.PlatformShareImpact = -300m;
            await scenario.Db.SaveChangesAsync();
        }

        var response = await scenario.Controller().CreateExternalPackageRefund(scenario.CashRequest(invalidTeacherShare ? 290m : 300.01m), default);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        await AssertUnchangedAsync(scenario);
    }

    private static async Task SeedLegacyFunding(RefundTestScenario scenario, int general, int recharge, int gift)
    {
        var source = await scenario.Db.SalesFinancialEffects.SingleAsync();
        source.PaidAmount = general;
        source.PromotionalAmount = recharge + gift;
        source.PlatformShareImpact = general - source.TeacherShareImpact;
        var fundingId = Guid.NewGuid();
        scenario.Db.TeacherFinancialEvents.Add(new TeacherFinancialEvent { SourceId = source.PurchaseOperationId,
            SourceType = TeacherFinancialSourceType.DirectPurchase, StudentId = source.StudentId,
            TargetId = source.TargetId, TargetType = source.TargetType, GrossAmount = source.GrossAmount,
            PaidAmount = general, PromotionalAmount = recharge + gift, IdempotencyKey = $"legacy:{fundingId}",
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { fundingOperationId = fundingId }) });
        AddLegacyUsage(scenario, fundingId, recharge, "DIGITAL_RECHARGE");
        if (gift > 0) AddLegacyUsage(scenario, fundingId, gift, "GRANTED");
        await scenario.Db.SaveChangesAsync();
    }

    private static void AddLegacyUsage(RefundTestScenario scenario, Guid fundingId, int amount, string outcome)
    {
        var issuance = new GiftIssuance { RequestId = Guid.NewGuid(), TargetType = GiftTargetType.TeacherBalance,
            TeacherId = scenario.Teacher.Id, Amount = amount, Reason = "Legacy funding", IssuedByUserId = scenario.Actor.Id };
        var recipient = new GiftRecipient { GiftIssuance = issuance, StudentId = scenario.Student.Id, OutcomeCode = outcome };
        var allocation = new PromotionalBalanceAllocation { GiftRecipient = recipient, StudentId = scenario.Student.Id,
            TeacherId = scenario.Teacher.Id, OriginalAmount = amount, ConsumedAmount = amount };
        scenario.Db.PromotionalBalanceUsages.Add(new PromotionalBalanceUsage { Allocation = allocation,
            GiftRecipient = recipient, PurchaseOperationId = fundingId, ContentType = CodeType.Package,
            ContentId = scenario.Grant.PackageId!.Value, Amount = amount });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StudentBalanceRefund_ReversalRemovesCreditOrRejectsSpentBalance(bool spent)
    {
        await using var scenario = await RefundTestScenario.CreateAsync();
        var operations = scenario.Operations();
        var refund = await operations.CreateRefundAsync(new(scenario.PurchaseId, "PurchaseOperation", scenario.Student.Id,
            null, 75m, 0m, (int)PlatformRefundMethod.StudentBalance, null, "Balance reversal test", null, scenario.Actor.Id), default);
        await operations.PostRefundAsync(refund.Id, "balance-reversal-source", scenario.Actor.Id, default);
        if (spent)
            await new BalanceService(scenario.Db, NullLogger<BalanceService>.Instance)
                .DeductBalance(scenario.Student.Id, 90m, "Student spent refunded balance", Guid.NewGuid());
        var handler = new ReversePlatformRefundCommandHandler(scenario.Db, new FinancialPostingService(scenario.Db),
            new BalanceService(scenario.Db, NullLogger<BalanceService>.Instance));
        var request = new ReversePlatformRefundCommand(refund.Id, scenario.Actor.Id, "تصحيح استرداد الرصيد");

        if (spent)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(request, default));
            await using var verifyDb = scenario.CreateDbContext();
            Assert.Equal(5m, (await verifyDb.StudentBalances.SingleAsync()).CurrentBalance);
            Assert.Equal(PlatformRefundStatus.Posted, (await verifyDb.PlatformRefunds.SingleAsync()).Status);
            Assert.Equal(JournalEntryStatus.Posted, (await verifyDb.JournalEntries.SingleAsync()).Status);
        }
        else
        {
            await using var concurrentDb = scenario.CreateDbContext();
            var concurrentHandler = new ReversePlatformRefundCommandHandler(concurrentDb, new FinancialPostingService(concurrentDb),
                new BalanceService(concurrentDb, NullLogger<BalanceService>.Instance));
            var reversals = await Task.WhenAll(handler.Handle(request, default), concurrentHandler.Handle(request, default));
            Assert.Equal(reversals[0].ReversalId, reversals[1].ReversalId);
            Assert.Single(reversals, reversal => !reversal.AlreadyApplied);
            var replay = await handler.Handle(request, default);
            Assert.Equal(reversals[0].ReversalId, replay.ReversalId);
            Assert.True(replay.AlreadyApplied);
            await using var verifyDb = scenario.CreateDbContext();
            Assert.Equal(20m, (await verifyDb.StudentBalances.SingleAsync()).CurrentBalance);
            Assert.Equal(2, await verifyDb.JournalEntries.CountAsync());
            Assert.Equal(2, await verifyDb.BalanceTransactions.CountAsync());
            Assert.Equal(0m, await verifyDb.BalanceTransactions.SumAsync(credit => credit.Amount));
            var debit = await verifyDb.BalanceTransactions.SingleAsync(credit => credit.TransactionType == "PlatformRefundReversal");
            Assert.Equal(-75m, debit.Amount);
            Assert.Equal(scenario.Actor.Id, debit.PerformedByUserId);
            Assert.Equal(PlatformRefundStatus.Reversed, (await verifyDb.PlatformRefunds.SingleAsync()).Status);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("490.01")]
    [InlineData("75.001")]
    public async Task ExternalCashRefund_InvalidAmountLeavesAccessAndMoneyUnchanged(string input)
    {
        await using var scenario = await RefundTestScenario.CreateAsync();
        var amount = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        var response = await scenario.Controller().CreateExternalPackageRefund(scenario.CashRequest(amount), default);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        await AssertUnchangedAsync(scenario);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalCashRefund_FailedPostingRollsBackCancellationAndRefund(bool closedPeriod)
    {
        await using var scenario = await RefundTestScenario.CreateAsync();
        var request = scenario.CashRequest();
        if (closedPeriod)
        {
            scenario.Db.AccountingPeriods.Add(new() { StartDate = DateTime.UtcNow.Date.AddDays(-1), EndDate = DateTime.UtcNow.Date.AddDays(1), Status = AccountingPeriodStatus.Closed });
            await scenario.Db.SaveChangesAsync();
        }
        else
        {
            scenario.Cashbox.IsActive = false;
            await scenario.Db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.Controller().CreateExternalPackageRefund(request, default));

        await AssertUnchangedAsync(scenario);
    }

    [Fact]
    public async Task LegacyBalanceCancellation_FailedSaveRollsBackTheClaimAndCredit()
    {
        await using var scenario = await RefundTestScenario.CreateAsync();
        var handler = new CancelPackageGrantCommandHandler(scenario.Db, new TeacherAccountingService(scenario.Db));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new(scenario.Grant.Id, true, Guid.NewGuid(), "Missing operator must not partially cancel"), default));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        await AssertUnchangedAsync(scenario);
    }

    [Fact]
    public async Task ExternalCashRefund_UsesAuthoritativeTeacherSplitFor290Egp()
    {
        await using var scenario = await RefundTestScenario.CreateAsync();

        var response = await scenario.Controller().CreateExternalPackageRefund(scenario.CashRequest(), default);

        Assert.IsType<OkObjectResult>(response.Result);
        await using var verifyDb = scenario.CreateDbContext();
        var refund = await verifyDb.PlatformRefunds.SingleAsync();
        Assert.Equal(290m, refund.TotalAmount);
        Assert.Equal(218.98m, refund.PlatformAmount);
        Assert.Equal(71.02m, refund.TeacherAmount);
        Assert.Equal(scenario.Teacher.Id, refund.TeacherId);
        Assert.Equal(20m, (await verifyDb.StudentBalances.SingleAsync()).CurrentBalance);
        Assert.False((await verifyDb.StudentAccessGrants.SingleAsync()).IsActive);
        var journal = await verifyDb.JournalEntries.Include(entry => entry.Lines).SingleAsync();
        Assert.Equal(290m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(290m, journal.Lines.Sum(line => line.Credit));
    }

    private static async Task AssertUnchangedAsync(RefundTestScenario scenario)
    {
        await using var verifyDb = scenario.CreateDbContext();
        Assert.True((await verifyDb.StudentAccessGrants.SingleAsync()).IsActive);
        Assert.Equal(20m, (await verifyDb.StudentBalances.SingleAsync()).CurrentBalance);
        Assert.Empty(await verifyDb.PlatformRefunds.ToListAsync());
        Assert.Empty(await verifyDb.JournalEntries.ToListAsync());
        Assert.Empty(await verifyDb.BalanceTransactions.ToListAsync());
        Assert.Empty(await verifyDb.AuditLogs.ToListAsync());
        Assert.DoesNotContain(await verifyDb.OutboxEvents.ToListAsync(),
            notification => notification.Type is "BalanceChanged" or "PackageAccessRevoked");
    }
}
