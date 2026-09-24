using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Migrations;
using NaderGorge.Infrastructure.Services.Finance;
using Npgsql;

namespace NaderGorge.Application.Tests.Finance;

public sealed class CodeBatchPostgresFactAttribute : FactAttribute
{
    public CodeBatchPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TEACHER_CODE_TEST_DB") is null)
            Skip = "Requires an isolated loopback TEACHER_CODE_TEST_DB.";
    }
}

public sealed class CodeBatchPostgresTests
{
    static CodeBatchPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    [CodeBatchPostgresFact]
    public async Task Migration_preserves_old_agreements_and_concurrent_receipts_cannot_overcollect_a_batch()
    {
        var connection = Environment.GetEnvironmentVariable("TEACHER_CODE_TEST_DB")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("teacher_code_finance_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        await using var db = Open(connection);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        var (actor, teacher, group) = await UnifiedCodeBatchAccountingTests.SeedBatchAsync(db);
        db.TeacherFinancialAgreements.RemoveRange(await db.TeacherFinancialAgreements.ToListAsync());
        foreach (var trigger in new[] { TeacherAgreementTrigger.ContentSale, TeacherAgreementTrigger.CodeDelivery, TeacherAgreementTrigger.CodeActivation })
            db.TeacherFinancialAgreements.Add(new() { TeacherId = teacher.Id, Trigger = trigger,
                AllocationMode = TeacherAgreementAllocationMode.PlatformFixedPerUnit, AllocationValue = 15m,
                PriceBasis = TeacherPriceBasis.NetAfterDiscount, EffectiveFrom = DateTime.UtcNow.AddDays(-1), Reason = "original agreement" });
        await db.SaveChangesAsync();
        var originalIds = await db.TeacherFinancialAgreements.Select(x => x.Id).ToArrayAsync();
        var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(migration);
        await db.Database.ExecuteSqlRawAsync(migration.Operations.OfType<SqlOperation>().First().Sql);
        db.ChangeTracker.Clear();
        Assert.Equal(3, await db.TeacherFinancialAgreements.CountAsync(x => originalIds.Contains(x.Id) && x.EffectiveTo != null));
        Assert.Single(await db.TeacherFinancialAgreements.Where(x => x.Trigger == TeacherAgreementTrigger.AllSources && x.EffectiveTo == null).ToListAsync());
        var terms = await db.CodeGroupFinancialTerms.SingleAsync();
        var quote = await CodeGroupFinanceQuote.CalculateAsync(db, group, terms, DateTime.UtcNow, CancellationToken.None);
        var confirmed = await new ConfirmCodeGroupDeliveryCommandHandler(db,
            new CodeGroupFinancialAccountingService(db, new TeacherAccountingService(db)), new FinancialPostingService(db))
            .Handle(new(actor.Id, group.Id, "Teacher", null, null, quote.Key), CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Success, confirmed.Status);
        var treasuryId = await db.TreasuryAccounts.Select(x => x.Id).FirstAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Receive(string key)
        {
            await using var context = Open(connection);
            await gate.Task;
            var response = await new CollectCodeGroupPaymentCommandHandler(context, new FinancialPostingService(context))
                .Handle(new(actor.Id, group.Id, new(1000m, treasuryId, key, key)), CancellationToken.None);
            Assert.Contains(response.Status, new[] { TeacherFinanceCommandStatus.Success, TeacherFinanceCommandStatus.Invalid });
        }
        var first = Receive("receipt-one");
        var second = Receive("receipt-two");
        gate.SetResult();
        await Task.WhenAll(first, second);
        db.ChangeTracker.Clear();
        var payment = Assert.Single(await db.CodeGroupDeliveryPayments.ToListAsync());
        Assert.Equal(1000m, payment.Amount);
        var snapshot = await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, CancellationToken.None);
        Assert.Equal(500m, snapshot!.CodeAmountDue);
        Assert.Equal(8500m, snapshot.Retained);
        Assert.Equal(0m, snapshot.NetPayable);
        Assert.Equal(0m, snapshot.BalanceDifference);
        Assert.Equal(500m, await db.JournalLines.Where(x => x.FinancialAccount.Code == "1200").SumAsync(x => x.Debit - x.Credit));
    }

    private static AppDbContext Open(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private sealed class ExposedMigration : UnifiedTeacherCodeCollections
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }
}
