using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260729193000_AddOutboxClaims")]
public partial class AddOutboxClaims : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL lock_timeout = '5s';

            ALTER TABLE outbox_events
                ADD COLUMN IF NOT EXISTS "ClaimedBy" character varying(120) NULL,
                ADD COLUMN IF NOT EXISTS "ClaimedAt" timestamp without time zone NULL,
                ADD COLUMN IF NOT EXISTS "LeaseExpiresAt" timestamp without time zone NULL,
                ADD COLUMN IF NOT EXISTS "NextAttemptAt" timestamp without time zone NULL;
            """);

        migrationBuilder.Sql("""
            SET lock_timeout = '5s';
            SET statement_timeout = '30min';
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            DROP INDEX CONCURRENTLY IF EXISTS
                "IX_outbox_events_dispatch_claim";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_outbox_events_dispatch_claim"
                ON outbox_events
                    ("ProcessedAt", "IsDeadLetter", "NextAttemptAt",
                     "LeaseExpiresAt", "CreatedAt");
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            RESET statement_timeout;
            RESET lock_timeout;
            """, suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The production rollback contract retains the forward-compatible
        // schema. Down is intentionally non-destructive.
    }
}
