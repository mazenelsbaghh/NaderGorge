using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllocateLiveSupportEventSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "live_support_event_sequence");
            migrationBuilder.Sql("""
                SELECT setval('live_support_event_sequence', GREATEST(
                    COALESCE((SELECT MAX("Sequence") FROM live_support_events), 1),
                    (extract(epoch FROM clock_timestamp()) * 10000000)::bigint + 621355968000000000), true);

                CREATE FUNCTION massar_next_support_event_sequence() RETURNS bigint
                LANGUAGE plpgsql AS $$
                DECLARE allocated bigint;
                BEGIN
                    -- Keep legacy tick cursors compatible while serializing allocations
                    -- across nodes. The session lock is held only for this function,
                    -- never while the caller waits to commit application rows.
                    PERFORM pg_advisory_lock(716620260912::bigint);
                    BEGIN
                        allocated := GREATEST(nextval('live_support_event_sequence'),
                            (extract(epoch FROM clock_timestamp()) * 10000000)::bigint + 621355968000000000);
                        PERFORM setval('live_support_event_sequence', allocated, true);
                    EXCEPTION WHEN OTHERS OR query_canceled THEN
                        PERFORM pg_advisory_unlock(716620260912::bigint);
                        RAISE;
                    END;
                    PERFORM pg_advisory_unlock(716620260912::bigint);
                    RETURN allocated;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION massar_next_support_event_sequence();");
            migrationBuilder.DropSequence(
                name: "live_support_event_sequence");
        }
    }
}
