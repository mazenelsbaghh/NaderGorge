using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Integration.Tests.LiveSupport;

namespace NaderGorge.Integration.Tests.Migrations;

public sealed class PerformanceSchemaCompatibilityTests
{
    private const string PreviousMigration =
        "20260729151000_RepairVideoTypeCodeGrantSchema";
    private const string OutboxClaimsMigration =
        "20260729193000_AddOutboxClaims";
    private static readonly string[] ClaimColumns =
    [
        "ClaimedBy",
        "ClaimedAt",
        "LeaseExpiresAt",
        "NextAttemptAt"
    ];

    [Fact]
    public async Task EmptyDatabase_AddsOutboxClaimsWithoutSecurityCacheSchema()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.Db.Database.EnsureDeletedAsync();

        await fixture.Db.GetService<IMigrator>().MigrateAsync();

        Assert.Equal(ClaimColumns.Order(), await OutboxClaimColumnsAsync(fixture));
        Assert.True(await DispatchClaimIndexExistsAsync(fixture));
        var cacheTables = await fixture.Db.Database.SqlQuery<string>($"""
            SELECT table_name AS "Value"
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND (
                lower(table_name) LIKE '%security%cache%'
                OR lower(table_name) LIKE 'user_security_state%'
              )
            """).ToListAsync();
        Assert.Empty(cacheTables);
    }

    [Fact]
    public async Task NMinusOneUpgrade_PreservesLegacyReadsWritesAndRetainedSchema()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.Db.Database.EnsureDeletedAsync();
        var migrator = fixture.Db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var legacyIds = new[]
        {
            await InsertLegacyOutboxAsync(fixture, "legacy.pending", null, 0, false),
            await InsertLegacyOutboxAsync(
                fixture,
                "legacy.retry",
                "representative failure",
                2,
                false),
            await InsertLegacyOutboxAsync(fixture, "legacy.dead", "dead", 4, true)
        };
        Assert.Equal(3, await LegacyRowCountAsync(fixture, legacyIds));

        await migrator.MigrateAsync();

        Assert.Equal(3, await LegacyRowCountAsync(fixture, legacyIds));
        var afterUpgradeId = await InsertLegacyOutboxAsync(
            fixture,
            "legacy.after-upgrade",
            null,
            0,
            false);
        Assert.Equal(4, await LegacyRowCountAsync(
            fixture,
            [.. legacyIds, afterUpgradeId]));
        Assert.Equal(ClaimColumns.Order(), await OutboxClaimColumnsAsync(fixture));

        Assert.Equal(ClaimColumns.Order(), await OutboxClaimColumnsAsync(fixture));
        Assert.True(await DispatchClaimIndexExistsAsync(fixture));
        var retainedSchemaId = await InsertLegacyOutboxAsync(
            fixture,
            "legacy.retained-schema",
            null,
            0,
            false);
        Assert.Equal(5, await LegacyRowCountAsync(
            fixture,
            [.. legacyIds, afterUpgradeId, retainedSchemaId]));

        await migrator.MigrateAsync();
        Assert.Equal(5, await LegacyRowCountAsync(
            fixture,
            [.. legacyIds, afterUpgradeId, retainedSchemaId]));
    }

    [Fact]
    public async Task ProductionLikeUpgrade_PreservesLegacyWebVitalsAndNMinusOneWrites()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.Db.Database.EnsureDeletedAsync();
        var migrator = fixture.Db.GetService<IMigrator>();
        await migrator.MigrateAsync(OutboxClaimsMigration);

        var existingId = await InsertLegacyWebVitalAsync(
            fixture,
            "/student/packages/legacy-id");

        await migrator.MigrateAsync();

        var upgraded = await LegacyWebVitalAsync(fixture, existingId);
        Assert.Equal("legacy", upgraded.MetricId);
        Assert.Equal("/unknown", upgraded.RouteTemplate);
        Assert.Equal("legacy", upgraded.ReleaseId);

        var nMinusOneWriteId = await InsertLegacyWebVitalAsync(
            fixture,
            "/student/dashboard");
        var nMinusOneWrite = await LegacyWebVitalAsync(
            fixture,
            nMinusOneWriteId);
        Assert.Equal("legacy", nMinusOneWrite.MetricId);
        Assert.Equal("/unknown", nMinusOneWrite.RouteTemplate);
    }

    private static async Task<Guid> InsertLegacyOutboxAsync(
        PostgresLiveSupportFixture fixture,
        string eventType,
        string? lastError,
        int retryCount,
        bool isDeadLetter)
    {
        var eventId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        await fixture.Db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO outbox_events
                ("Id", "Type", "PayloadJson", "TargetGroup", "TargetUserId",
                 "ProcessedAt", "RetryCount", "LastError", "IsDeadLetter",
                 "CreatedAt", "UpdatedAt")
            VALUES
                ({{eventId}}, {{eventType}}, '{}', 'students', NULL,
                 NULL, {{retryCount}}, {{lastError}}, {{isDeadLetter}},
                 {{createdAt}}, NULL)
            """);
        return eventId;
    }

    private static async Task<int> LegacyRowCountAsync(
        PostgresLiveSupportFixture fixture,
        IReadOnlyCollection<Guid> eventIds) =>
        await fixture.Db.Database.SqlQuery<int>($$"""
            SELECT COUNT(*)::integer AS "Value"
            FROM outbox_events
            WHERE "Id" = ANY ({{eventIds.ToArray()}})
              AND "Type" LIKE 'legacy.%'
              AND "PayloadJson" = '{}'
            """).SingleAsync();

    private static async Task<Guid> InsertLegacyWebVitalAsync(
        PostgresLiveSupportFixture fixture,
        string pageUrl)
    {
        var metricId = Guid.NewGuid();
        await fixture.Db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO web_vitals_metrics
                ("Id", "MetricName", "Value", "Rating", "PageUrl",
                 "UserAgent", "CreatedAt", "UpdatedAt")
            VALUES
                ({{metricId}}, 'LCP', 2200, 'good', {{pageUrl}},
                 'legacy-agent', {{DateTime.UtcNow}}, NULL)
            """);
        return metricId;
    }

    private static Task<LegacyWebVital> LegacyWebVitalAsync(
        PostgresLiveSupportFixture fixture,
        Guid metricId) =>
        fixture.Db.Database.SqlQuery<LegacyWebVital>($$"""
            SELECT
                "MetricId",
                "RouteTemplate",
                "ReleaseId"
            FROM web_vitals_metrics
            WHERE "Id" = {{metricId}}
            """).SingleAsync();

    private static async Task<string[]> OutboxClaimColumnsAsync(
        PostgresLiveSupportFixture fixture)
    {
        var columns = await fixture.Db.Database.SqlQuery<string>($"""
            SELECT column_name AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'outbox_events'
              AND column_name = ANY ({ClaimColumns})
            ORDER BY column_name
            """).ToArrayAsync();
        return columns.Order().ToArray();
    }

    private static async Task<bool> DispatchClaimIndexExistsAsync(
        PostgresLiveSupportFixture fixture) =>
        await fixture.Db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename = 'outbox_events'
                  AND indexname = 'IX_outbox_events_dispatch_claim'
            ) AS "Value"
            """).SingleAsync();

    private sealed record LegacyWebVital(
        string MetricId,
        string RouteTemplate,
        string ReleaseId);
}
