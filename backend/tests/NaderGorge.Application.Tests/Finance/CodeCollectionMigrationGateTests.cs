using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Migrations;
using Npgsql;

namespace NaderGorge.Application.Tests.Finance;

public sealed class CodeCollectionMigrationGateTests
{
    [FinanceRepairTheory]
    [InlineData("valid")]
    [InlineData("account")]
    [InlineData("old-agreement")]
    [InlineData("new-agreement")]
    [InlineData("old-id")]
    [InlineData("missing-agreement")]
    [InlineData("delivery")]
    [InlineData("retained")]
    public async Task Gate_accepts_exact_backfill_and_rejects_unexpected_history_changes(string scenario)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FINANCE_REPAIR_TEST_DB")!);
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost" });
        connection.Database = "code_collection_gate_test";
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (actor, teacher, group) = await UnifiedCodeBatchAccountingTests.SeedBatchAsync(db);
        db.TeacherFinancialAgreements.RemoveRange(await db.TeacherFinancialAgreements.ToListAsync());
        foreach (var trigger in new[] { TeacherAgreementTrigger.ContentSale, TeacherAgreementTrigger.CodeDelivery, TeacherAgreementTrigger.CodeActivation })
            db.TeacherFinancialAgreements.Add(new() { TeacherId = teacher.Id, Trigger = trigger,
                AllocationMode = TeacherAgreementAllocationMode.PlatformFixedPerUnit, AllocationValue = 15m,
                PriceBasis = TeacherPriceBasis.NetAfterDiscount, EffectiveFrom = DateTime.UtcNow.AddDays(-1), Reason = "original", CreatedByUserId = actor.Id });
        db.CodeGroupDeliveryConfirmations.Add(new() { CodeGroupId = group.Id, Recipient = "Original recipient",
            ConfirmedByUserId = actor.Id, ConfirmedAt = DateTime.UtcNow, IdempotencyKey = "original-delivery" });
        db.TeacherFinancialEvents.Add(new() { IdempotencyKey = "original-sale", OccurredAt = DateTime.UtcNow,
            Allocations = [new() { TeacherId = teacher.Id, TeacherShareAmount = 10m }] });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId"='20260924152917_UnifiedTeacherCodeCollections';
            DELETE FROM financial_accounts WHERE "Code"='1200';
            ALTER TABLE teacher_financial_allocations DROP COLUMN "RetainedByTeacher";
            ALTER TABLE code_group_delivery_confirmations DROP COLUMN "PlatformAmountDue", DROP COLUMN "TeacherRetainedAmount";
            """);
        await db.Database.ExecuteSqlRawAsync(GateSql("FUNDING_SNAPSHOT_SQL") + GateSql("CODE_COLLECTION_SNAPSHOT_SQL"));
        var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(migration);
        await db.Database.ExecuteSqlRawAsync(migration.Operations.OfType<SqlOperation>().First().Sql);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE teacher_financial_allocations ADD COLUMN "RetainedByTeacher" boolean NOT NULL DEFAULT false;
            ALTER TABLE code_group_delivery_confirmations ADD COLUMN "PlatformAmountDue" numeric(18,2), ADD COLUMN "TeacherRetainedAmount" numeric(18,2);
            INSERT INTO "__EFMigrationsHistory" VALUES ('20260924152917_UnifiedTeacherCodeCollections','9.0.6');
            """);
        var tamper = scenario switch
        {
            "account" => "UPDATE financial_accounts SET \"Name\"='Unexpected' WHERE \"Code\"='1200'",
            "old-agreement" => "UPDATE teacher_financial_agreements SET \"AllocationValue\"=99 WHERE \"Trigger\"=0",
            "new-agreement" => "UPDATE teacher_financial_agreements SET \"AllocationValue\"=99 WHERE \"Trigger\"=3",
            "old-id" => "UPDATE teacher_financial_agreements SET \"Id\"=gen_random_uuid() WHERE \"Trigger\"=0",
            "missing-agreement" => "DELETE FROM teacher_financial_agreements WHERE \"Trigger\"=3",
            "delivery" => "UPDATE code_group_delivery_confirmations SET \"PlatformAmountDue\"=100",
            "retained" => "UPDATE teacher_financial_allocations SET \"RetainedByTeacher\"=true",
            _ => null
        };
        if (tamper is not null) await db.Database.ExecuteSqlRawAsync(tamper);
        var validation = GateSql("CODE_COLLECTION_VALIDATION_SQL") + GateSql("FUNDING_VALIDATION_SQL");
        if (scenario == "valid")
        {
            await db.Database.ExecuteSqlRawAsync(validation);
            Assert.Equal(4, await db.TeacherFinancialAgreements.CountAsync());
            Assert.Equal(3, await db.TeacherFinancialAgreements.CountAsync(x => x.EffectiveTo != null));
            Assert.False(await db.TeacherFinancialAllocations.Select(x => x.RetainedByTeacher).SingleAsync());
            // A later release must validate the already-applied migration without
            // inserting another agreement or accepting further historical changes.
            await db.Database.ExecuteSqlRawAsync("DROP SCHEMA massar_gate_codes CASCADE;");
            await db.Database.ExecuteSqlRawAsync(GateSql("FUNDING_SNAPSHOT_SQL") + GateSql("CODE_COLLECTION_SNAPSHOT_SQL"));
            await db.Database.ExecuteSqlRawAsync(validation);
            Assert.Equal(4, await db.TeacherFinancialAgreements.CountAsync());
        }
        else
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(validation));
            Assert.Contains("gate:", error.MessageText);
        }
        await transaction.RollbackAsync();
    }

    private static string GateSql(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "deploy/production/scripts/prepare_release_migration_gate.py"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "deploy/production/scripts/prepare_release_migration_gate.py"));
        var marker = name + " = r" + new string('"', 3);
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        return source[start..source.IndexOf(new string('"', 3), start, StringComparison.Ordinal)];
    }

    private sealed class ExposedMigration : UnifiedTeacherCodeCollections
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }
}
