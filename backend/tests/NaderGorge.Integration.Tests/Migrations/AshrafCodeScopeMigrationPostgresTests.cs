using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Infrastructure.Migrations;
using Npgsql;

namespace NaderGorge.Integration.Tests.Migrations;

public sealed class AshrafCodeScopeMigrationPostgresTests
{
    [Fact]
    public async Task ReviewedBatch_ConservesValuePreservesSpendingAndDoesNotMoveFundsTwice()
    {
        await using var connection = await OpenTestConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        await Seed(connection, transaction);
        await Execute(connection, transaction, GateSql("CODE_SCOPE_SNAPSHOT_SQL"));
        await Execute(connection, transaction, MigrationSql());
        await Execute(connection, transaction, GateSql("CODE_SCOPE_VALIDATION_SQL"));
        await Execute(connection, transaction, GateSql("CODE_SCOPE_SNAPSHOT_SQL"));
        await Execute(connection, transaction, MigrationSql());
        await Execute(connection, transaction, GateSql("CODE_SCOPE_VALIDATION_SQL"));

        Assert.Equal(0m, await Scalar(connection, transaction, "SELECT sum(\"CurrentBalance\") FROM student_balances"));
        Assert.Equal(2550m, await Scalar(connection, transaction, "SELECT sum(\"AvailableAmount\") FROM promotional_balance_allocations"));
        Assert.Equal(-2550m, await Scalar(connection, transaction, "SELECT sum(\"Amount\") FROM balance_transactions WHERE \"TransactionType\"='CodeScopeCorrection'"));
        Assert.Equal(55m, await Scalar(connection, transaction, "SELECT count(*) FROM balance_transactions WHERE \"TransactionType\"='CodeRedemption'"));
        Assert.Equal(55m, await Scalar(connection, transaction, "SELECT count(*) FROM access_codes WHERE \"IsConsumed\""));
        Assert.Equal(45m, await Scalar(connection, transaction, "SELECT count(*) FROM access_codes WHERE NOT \"IsConsumed\""));
        Assert.Equal(44500m, await Scalar(connection, transaction, "SELECT -sum(\"Amount\") FROM balance_transactions WHERE \"TransactionType\"='ContentPurchase'"));
        Assert.Equal(300m, await Scalar(connection, transaction, "SELECT sum(\"Amount\") FROM balance_transactions WHERE \"TransactionType\"='Refund'"));
        Assert.Equal(3m, await Scalar(connection, transaction, "SELECT count(*) FROM gift_issuances"));
        Assert.Equal(1m, await Scalar(connection, transaction, "SELECT count(*) FROM audit_logs"));
        Assert.Equal(1m, await Scalar(connection, transaction, "SELECT count(*) FROM code_groups WHERE \"TeacherId\"='73cf9e05-d068-4a0c-b8e6-dcba16554417'"));
    }

    // A student spent 850 EGP during the first release preparation on 2026-10-24.
    [Theory]
    [InlineData(0)]
    [InlineData(125)]
    public async Task SpendingAfterReview_TransfersOnlyActualRemainderWithoutUndoingPurchase(int remainder)
    {
        await using var connection = await OpenTestConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        await Seed(connection, transaction);
        await using (var spending = new NpgsqlCommand("""
            UPDATE student_balances SET "CurrentBalance"=@remainder WHERE "UserId"=md5('student54')::uuid;
            INSERT INTO balance_transactions ("Id","StudentBalanceId","Amount","TransactionType")
            VALUES (gen_random_uuid(),md5('student54')::uuid,@remainder-850,'ContentPurchase');
            """, connection, transaction))
        {
            spending.Parameters.AddWithValue("remainder", remainder);
            await spending.ExecuteNonQueryAsync();
        }
        await Execute(connection, transaction, GateSql("CODE_SCOPE_SNAPSHOT_SQL"));
        await Execute(connection, transaction, MigrationSql());
        await Execute(connection, transaction, GateSql("CODE_SCOPE_VALIDATION_SQL"));
        Assert.Equal(1700m + remainder, await Scalar(connection, transaction, "SELECT sum(\"AvailableAmount\") FROM promotional_balance_allocations"));
        Assert.Equal(0m, await Scalar(connection, transaction, "SELECT sum(\"CurrentBalance\") FROM student_balances"));
        Assert.Equal(45350m - remainder, await Scalar(connection, transaction, "SELECT -sum(\"Amount\") FROM balance_transactions WHERE \"TransactionType\"='ContentPurchase'"));
    }

    [Fact]
    public async Task EntireRemainderSpent_RepairsOwnershipWithoutCreatingAnyBalanceCredit()
    {
        await using var connection = await OpenTestConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        await Seed(connection, transaction);
        await Execute(connection, transaction, """
            INSERT INTO balance_transactions ("Id","StudentBalanceId","Amount","TransactionType")
              SELECT gen_random_uuid(),"Id",-"CurrentBalance",'ContentPurchase'
              FROM student_balances WHERE "CurrentBalance">0;
            UPDATE student_balances SET "CurrentBalance"=0;
            """);
        await Execute(connection, transaction, GateSql("CODE_SCOPE_SNAPSHOT_SQL"));
        await Execute(connection, transaction, MigrationSql());
        await Execute(connection, transaction, GateSql("CODE_SCOPE_VALIDATION_SQL"));
        Assert.Equal(0m, await Scalar(connection, transaction, "SELECT count(*) FROM gift_issuances"));
        Assert.Equal(0m, await Scalar(connection, transaction, "SELECT count(*) FROM balance_transactions WHERE \"TransactionType\"='CodeScopeCorrection'"));
        Assert.Equal(1m, await Scalar(connection, transaction, "SELECT count(*) FROM code_groups WHERE \"TeacherId\"='73cf9e05-d068-4a0c-b8e6-dcba16554417'"));
    }

    [Theory]
    [InlineData("UPDATE student_balances SET \"CurrentBalance\"=1 WHERE \"UserId\"=md5('student1')::uuid;")]
    [InlineData("UPDATE balance_transactions SET \"Amount\"=851 WHERE \"ReferenceId\"=md5('code1')::uuid AND \"TransactionType\"='CodeRedemption';")]
    [InlineData("UPDATE promotional_balance_allocations SET \"AvailableAmount\"=849,\"ConsumedAmount\"=1;")]
    [InlineData("UPDATE gift_recipients SET \"StudentId\"=md5('student1')::uuid;")]
    [InlineData("UPDATE audit_logs SET \"NewValues\"='{}';")]
    public async Task ReleaseGate_RejectsUnexpectedChangesEvenWhenTableCountsMatch(string tamper)
    {
        await using var connection = await OpenTestConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        await Seed(connection, transaction);
        await Execute(connection, transaction, GateSql("CODE_SCOPE_SNAPSHOT_SQL"));
        await Execute(connection, transaction, MigrationSql());
        await Execute(connection, transaction, tamper);
        await Assert.ThrowsAsync<PostgresException>(() => Execute(connection, transaction, GateSql("CODE_SCOPE_VALIDATION_SQL")));
    }

    [Theory]
    [InlineData("DELETE FROM balance_transactions WHERE \"TransactionType\"='CodeRedemption' AND \"ReferenceId\"=md5('code1')::uuid;")]
    [InlineData("INSERT INTO balance_transactions (\"Id\",\"StudentBalanceId\",\"Amount\",\"TransactionType\") VALUES (gen_random_uuid(),md5('student1')::uuid,1,'CodeRedemption'),(gen_random_uuid(),md5('student1')::uuid,-1,'ContentPurchase');")]
    [InlineData("INSERT INTO balance_transactions (\"Id\",\"StudentBalanceId\",\"Amount\",\"TransactionType\") VALUES (gen_random_uuid(),md5('student1')::uuid,1,'Refund'),(gen_random_uuid(),md5('student1')::uuid,-1,'ContentPurchase');")]
    [InlineData("UPDATE access_codes SET \"IsConsumed\"=true,\"ConsumedByUserId\"=md5('student56')::uuid WHERE \"Id\"=md5('code56')::uuid;")]
    public async Task ChangedReview_RejectsWholeCorrectionWithoutRechargingOrDebitingAnyone(string change)
    {
        await using var connection = await OpenTestConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        await Seed(connection, transaction);
        await Execute(connection, transaction, change);
        var originalBalance = await Scalar(connection, transaction, "SELECT sum(\"CurrentBalance\") FROM student_balances");
        await transaction.SaveAsync("before_repair");
        await Assert.ThrowsAsync<PostgresException>(() => Execute(connection, transaction, MigrationSql()));
        await transaction.RollbackAsync("before_repair");
        Assert.Equal(originalBalance, await Scalar(connection, transaction, "SELECT sum(\"CurrentBalance\") FROM student_balances"));
        Assert.Equal(0m, await Scalar(connection, transaction, "SELECT count(*) FROM promotional_balance_allocations"));
        Assert.Equal(0m, await Scalar(connection, transaction, "SELECT count(*) FROM code_groups WHERE \"TeacherId\" IS NOT NULL"));
        Assert.Equal(0m, await Scalar(connection, transaction, "SELECT count(*) FROM audit_logs"));
    }

    private static async Task<NpgsqlConnection> OpenTestConnection()
    {
        var configured = Environment.GetEnvironmentVariable("ASHRAF_SCOPE_TEST_DB")
            ?? throw new InvalidOperationException("Set ASHRAF_SCOPE_TEST_DB to the disposable ashraf_scope_test database.");
        if (new NpgsqlConnectionStringBuilder(configured).Database != "ashraf_scope_test")
            throw new InvalidOperationException("These tests require database ashraf_scope_test.");
        var connection = new NpgsqlConnection(configured);
        await connection.OpenAsync();
        return connection;
    }

    private static string MigrationSql()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(RepairAshrafBalanceCodeScope).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new RepairAshrafBalanceCodeScope(), [builder]);
        return Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
    }

    private static string GateSql(string constant)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "deploy/production/scripts/prepare_release_migration_gate.py")))
            directory = directory.Parent;
        var source = File.ReadAllText(Path.Combine(directory!.FullName, "deploy/production/scripts/prepare_release_migration_gate.py"));
        var match = Regex.Match(source, constant + " = r\"\"\"\\r?\\n(.*?)\"\"\"", RegexOptions.Singleline);
        Assert.True(match.Success, "The production release gate SQL must be present.");
        return match.Groups[1].Value;
    }

    private static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<decimal> Scalar(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static Task Seed(NpgsqlConnection connection, NpgsqlTransaction transaction) => Execute(connection, transaction, """
        CREATE TEMP TABLE teacher_profiles ("Id" uuid PRIMARY KEY) ON COMMIT DROP;
        CREATE TEMP TABLE code_groups ("Id" uuid PRIMARY KEY, "TeacherId" uuid REFERENCES teacher_profiles,
            "CodeType" integer, "BalanceAmount" numeric, "Name" text, "ExpiresAt" timestamp,
            "CreatedByUserId" uuid, "UpdatedAt" timestamp) ON COMMIT DROP;
        CREATE TEMP TABLE access_codes ("Id" uuid PRIMARY KEY, "CodeGroupId" uuid REFERENCES code_groups,
            "IsConsumed" boolean, "ConsumedByUserId" uuid) ON COMMIT DROP;
        CREATE TEMP TABLE student_balances ("Id" uuid PRIMARY KEY, "UserId" uuid UNIQUE,
            "CurrentBalance" numeric CHECK ("CurrentBalance">=0), "Version" bigint DEFAULT 0, "UpdatedAt" timestamp) ON COMMIT DROP;
        CREATE TEMP TABLE balance_transactions ("Id" uuid PRIMARY KEY, "StudentBalanceId" uuid REFERENCES student_balances,
            "Amount" numeric NOT NULL, "BalanceAfter" numeric, "TransactionType" text, "ReferenceId" uuid,
            "Description" text, "PerformedByUserId" uuid, "CreatedAt" timestamp) ON COMMIT DROP;
        CREATE TEMP TABLE gift_issuances ("Id" uuid PRIMARY KEY, "RequestId" uuid UNIQUE, "TargetType" integer,
            "TeacherId" uuid REFERENCES teacher_profiles, "Amount" numeric CHECK ("Amount">0), "Reason" text,
            "IssuedByUserId" uuid, "Status" integer, "CreatedAt" timestamp, "ExpiresAt" timestamp,
            "PackageId" uuid, "TermId" uuid, "ContentSectionId" uuid, "LessonId" uuid,
            "LessonVideoId" uuid, "ExamId" uuid, "MaxUses" integer) ON COMMIT DROP;
        CREATE TEMP TABLE gift_recipients ("Id" uuid PRIMARY KEY, "GiftIssuanceId" uuid REFERENCES gift_issuances,
            "StudentId" uuid, "Status" integer, "OutcomeCode" text, "OutcomeMessage" text, "UsesConsumed" integer,
            "CreatedAt" timestamp, "RevokedAt" timestamp, "RevokedByUserId" uuid, "RevocationReason" text) ON COMMIT DROP;
        CREATE TEMP TABLE promotional_balance_allocations ("Id" uuid PRIMARY KEY,
            "GiftRecipientId" uuid UNIQUE REFERENCES gift_recipients, "StudentId" uuid, "TeacherId" uuid REFERENCES teacher_profiles,
            "OriginalAmount" numeric, "AvailableAmount" numeric, "ConsumedAmount" numeric, "ExpiredAmount" numeric,
            "RevokedAmount" numeric, "PurchaseCount" integer, "Status" integer, "CreatedAt" timestamp,
            "ExpiresAt" timestamp, "MaxPurchaseCount" integer,
            CHECK ("OriginalAmount"="AvailableAmount"+"ConsumedAmount"+"ExpiredAmount"+"RevokedAmount")) ON COMMIT DROP;
        CREATE TEMP TABLE outbox_events ("Id" uuid PRIMARY KEY, "Type" text, "TargetUserId" text,
            "PayloadJson" text, "RetryCount" integer, "IsDeadLetter" boolean, "CreatedAt" timestamp, "ProcessedAt" timestamp) ON COMMIT DROP;
        CREATE TEMP TABLE audit_logs ("Id" uuid PRIMARY KEY, "Action" text, "EntityType" text,
            "EntityId" uuid, "ActorType" text, "RequestId" text, "Reason" text,
            "OldValues" text, "NewValues" text, "CreatedAt" timestamp, "PerformedByUserId" uuid) ON COMMIT DROP;
        INSERT INTO teacher_profiles VALUES ('73cf9e05-d068-4a0c-b8e6-dcba16554417');
        INSERT INTO code_groups VALUES ('b134dca5-5ddb-4b01-a061-e9a56b6ac23a',NULL,6,850,
            'اكواد اشرف بكر ب 850',NULL,gen_random_uuid(),NULL);
        INSERT INTO access_codes SELECT md5('code'||n)::uuid,'b134dca5-5ddb-4b01-a061-e9a56b6ac23a',
            n<=55, CASE WHEN n<=55 THEN md5('student'||n)::uuid END FROM generate_series(1,100) n;
        INSERT INTO student_balances ("Id","UserId","CurrentBalance")
            SELECT md5('student'||n)::uuid,md5('student'||n)::uuid,CASE WHEN n<=52 THEN 0 ELSE 850 END FROM generate_series(1,55) n;
        INSERT INTO balance_transactions ("Id","StudentBalanceId","Amount","TransactionType","ReferenceId")
            SELECT gen_random_uuid(),md5('student'||n)::uuid,850,'CodeRedemption',md5('code'||n)::uuid FROM generate_series(1,55) n;
        INSERT INTO balance_transactions ("Id","StudentBalanceId","Amount","TransactionType")
            SELECT gen_random_uuid(),md5('student'||n)::uuid,-850,'ContentPurchase' FROM generate_series(1,52) n;
        INSERT INTO balance_transactions ("Id","StudentBalanceId","Amount","TransactionType") VALUES
            (gen_random_uuid(),md5('student1')::uuid,-100,'ContentPurchase'),
            (gen_random_uuid(),md5('student1')::uuid,100,'Refund'),
            (gen_random_uuid(),md5('student53')::uuid,-200,'ContentPurchase'),
            (gen_random_uuid(),md5('student53')::uuid,200,'Refund');
        """);
}
