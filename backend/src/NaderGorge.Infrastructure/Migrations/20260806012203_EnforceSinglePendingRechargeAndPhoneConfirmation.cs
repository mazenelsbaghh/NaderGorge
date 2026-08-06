using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSinglePendingRechargeAndPhoneConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalSenderPhoneNumber",
                table: "recharge_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH ranked_pending AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "UserId"
                               ORDER BY "CreatedAt" DESC, "Id" DESC) AS row_number
                    FROM recharge_requests
                    WHERE "Status" = 0
                )
                UPDATE recharge_requests AS request
                SET "Status" = 3,
                    "RejectionReason" = COALESCE(
                        NULLIF(request."RejectionReason", ''),
                        'أغلقه النظام عند تطبيق قاعدة الطلب المعلق الواحد'),
                    "ResolvedAt" = COALESCE(request."ResolvedAt", CURRENT_TIMESTAMP),
                    "ReservationExpiresAt" = NULL
                FROM ranked_pending
                WHERE request."Id" = ranked_pending."Id"
                  AND ranked_pending.row_number > 1;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSenderPhoneConfirmation",
                table: "recharge_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SenderPhoneConfirmedAt",
                table: "recharge_requests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_UserId_pending",
                table: "recharge_requests",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_recharge_requests_UserId_pending",
                table: "recharge_requests");

            migrationBuilder.DropColumn(
                name: "OriginalSenderPhoneNumber",
                table: "recharge_requests");

            migrationBuilder.DropColumn(
                name: "RequiresSenderPhoneConfirmation",
                table: "recharge_requests");

            migrationBuilder.DropColumn(
                name: "SenderPhoneConfirmedAt",
                table: "recharge_requests");

        }
    }
}
