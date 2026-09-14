using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomingSmsTransferIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incoming_sms_logs_WalletId",
                table: "incoming_sms_logs");

            migrationBuilder.AddColumn<string>(
                name: "TransferReference",
                table: "incoming_sms_logs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.Sql("""
                WITH parsed AS (
                    SELECT "Id", "WalletId",
                           (regexp_match("Body", '(?:رقم[[:space:]]+العملية|transaction[[:space:]]*(?:number|id))[[:space:]]*[:：]?[[:space:]]*([0-9]+)', 'i'))[1] AS reference
                    FROM incoming_sms_logs
                ), unique_references AS (
                    SELECT "WalletId", reference
                    FROM parsed
                    WHERE reference IS NOT NULL
                    GROUP BY "WalletId", reference
                    HAVING count(*) = 1
                )
                UPDATE incoming_sms_logs AS log
                SET "TransferReference" = parsed.reference
                FROM parsed
                JOIN unique_references unique_ref
                  ON unique_ref."WalletId" = parsed."WalletId" AND unique_ref.reference = parsed.reference
                WHERE log."Id" = parsed."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_incoming_sms_logs_WalletId_TransferReference",
                table: "incoming_sms_logs",
                columns: new[] { "WalletId", "TransferReference" },
                unique: true,
                filter: "\"TransferReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incoming_sms_logs_WalletId_TransferReference",
                table: "incoming_sms_logs");

            migrationBuilder.DropColumn(
                name: "TransferReference",
                table: "incoming_sms_logs");

            migrationBuilder.CreateIndex(
                name: "IX_incoming_sms_logs_WalletId",
                table: "incoming_sms_logs",
                column: "WalletId");
        }
    }
}
