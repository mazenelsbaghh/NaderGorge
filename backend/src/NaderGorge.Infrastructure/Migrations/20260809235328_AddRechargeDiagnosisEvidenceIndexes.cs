using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRechargeDiagnosisEvidenceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET lock_timeout = '5s';
                SET statement_timeout = '30min';
                """, suppressTransaction: true);
            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS
                    "IX_incoming_sms_logs_ParsedAmount_ReceivedAt";
                """, suppressTransaction: true);
            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY
                    "IX_incoming_sms_logs_ParsedAmount_ReceivedAt"
                    ON incoming_sms_logs ("ParsedAmount", "ReceivedAt")
                    WHERE "ParsedAmount" IS NOT NULL
                      AND "ParsedSenderPhone" IS NOT NULL;
                """, suppressTransaction: true);
            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS
                    "IX_incoming_sms_logs_ParsedSenderPhone_ReceivedAt";
                """, suppressTransaction: true);
            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY
                    "IX_incoming_sms_logs_ParsedSenderPhone_ReceivedAt"
                    ON incoming_sms_logs ("ParsedSenderPhone", "ReceivedAt")
                    WHERE "ParsedAmount" IS NOT NULL
                      AND "ParsedSenderPhone" IS NOT NULL;
                """, suppressTransaction: true);
            migrationBuilder.Sql("""
                RESET statement_timeout;
                RESET lock_timeout;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS
                    "IX_incoming_sms_logs_ParsedAmount_ReceivedAt";
                """, suppressTransaction: true);
            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS
                    "IX_incoming_sms_logs_ParsedSenderPhone_ReceivedAt";
                """, suppressTransaction: true);
        }
    }
}
