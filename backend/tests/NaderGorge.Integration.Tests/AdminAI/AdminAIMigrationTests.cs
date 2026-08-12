using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Infrastructure.Migrations;
using Npgsql;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIMigrationTests
{
    [Fact]
    public void Up_IsAdditiveAndCreatesOnlyAdminAITables()
    {
        var operations = new MigrationProbe().CollectUpOperations();
        var creates = operations.OfType<CreateTableOperation>().ToArray();

        Assert.Equal(13, creates.Length);
        Assert.All(creates, operation => Assert.StartsWith("admin_ai_", operation.Name));
        Assert.DoesNotContain(operations, operation => operation is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        Assert.DoesNotContain(operations, operation => operation is InsertDataOperation);
    }

    [Fact]
    public void Up_UsesRestrictForEveryAdminAIForeignKey()
    {
        var creates = new MigrationProbe().CollectUpOperations().OfType<CreateTableOperation>();
        var foreignKeys = creates.SelectMany(table => table.ForeignKeys).ToArray();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, key => Assert.Equal(ReferentialAction.Restrict, key.OnDelete));
    }

    [Fact]
    public void Up_ContainsRequiredPartialUniqueChecksAndConcurrencyColumns()
    {
        var operations = new MigrationProbe().CollectUpOperations();
        var indexes = operations.OfType<CreateIndexOperation>().ToArray();
        Assert.Contains(indexes, x => x.IsUnique && x.Filter == "\"Status\" = 1" && x.Table == "admin_ai_capability_baselines");
        Assert.Contains(indexes, x => x.IsUnique && x.Filter == "\"Status\" = 1" && x.Table == "admin_ai_sensitive_policy_versions");
        var tables = operations.OfType<CreateTableOperation>().ToArray();
        Assert.Contains(tables.SelectMany(x => x.CheckConstraints), x => x.Name == "ck_admin_ai_turn_budgets");
        Assert.Contains(tables.Single(x => x.Name == "admin_ai_conversations").Columns, x => x.Name == "Version" && !x.IsNullable);
    }

    [Fact]
    public async Task CleanAndExistingPostgresDatabase_PreserveDataAndEnforceSchemaContracts()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.True(migrations.Length >= 2, "The AdminAI migration must have a predecessor.");
        var adminAIMigrationIndex = Array.FindIndex(migrations, migration => migration.EndsWith("_AddAdminAIAgent", StringComparison.Ordinal));
        Assert.True(adminAIMigrationIndex > 0, "The AdminAI migration and its predecessor must exist.");

        // Existing-database path: migrate to the prior production schema, add a
        // sentinel business row/table, then upgrade. The additive AdminAI
        // migration must leave it untouched.
        await db.Database.MigrateAsync(migrations[adminAIMigrationIndex - 1]);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE admin_ai_existing_data_sentinel (id integer PRIMARY KEY, value text NOT NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO admin_ai_existing_data_sentinel (id, value) VALUES (1, 'preserve-me')");
        await db.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        Assert.Equal("preserve-me", await ScalarAsync<string>(connection,
            "SELECT value FROM admin_ai_existing_data_sentinel WHERE id = 1"));
        Assert.Equal(14L, await ScalarAsync<long>(connection,
            "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'admin_ai_%' AND tablename <> 'admin_ai_existing_data_sentinel'"));

        var nonRestrictForeignKeys = await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            WHERE c.contype = 'f'
              AND t.relname LIKE 'admin_ai_%'
              AND c.confdeltype <> 'r'
            """);
        Assert.Equal(0L, nonRestrictForeignKeys);

        var activeUniqueIndexes = await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename IN ('admin_ai_capability_baselines', 'admin_ai_sensitive_policy_versions')
              AND indexdef ILIKE '%UNIQUE%'
              AND indexdef LIKE '%WHERE ("Status" = 1)%'
            """);
        Assert.Equal(2L, activeUniqueIndexes);

        var requiredChecks = await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_constraint
            WHERE contype = 'c'
              AND conname IN (
                'ck_admin_ai_baseline_counts',
                'ck_admin_ai_conversation_version',
                'ck_admin_ai_turn_budgets',
                'ck_admin_ai_step_bounds',
                'ck_admin_ai_read_bounds')
            """);
        Assert.Equal(5L, requiredChecks);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    private sealed class MigrationProbe : AddAdminAIAgent
    {
        public IReadOnlyList<MigrationOperation> CollectUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            base.Up(builder);
            return builder.Operations;
        }
    }
}
