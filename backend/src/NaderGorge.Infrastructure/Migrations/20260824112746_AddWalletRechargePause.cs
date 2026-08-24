using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletRechargePause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRechargePaused",
                table: "digital_wallets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RechargePauseMessage",
                table: "digital_wallets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RechargeResumeAt",
                table: "digital_wallets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE digital_wallets
                SET "IsRechargePaused" = TRUE,
                    "RechargePauseMessage" = 'التحويل متوقف مؤقتًا لحين تفعيل رقم التحويل الجديد. متوقع رجوع الخدمة خلال 24 ساعة. يرجى المحاولة لاحقًا.',
                    "RechargeResumeAt" = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC') + INTERVAL '24 hours'
                WHERE "IsActive" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRechargePaused",
                table: "digital_wallets");

            migrationBuilder.DropColumn(
                name: "RechargePauseMessage",
                table: "digital_wallets");

            migrationBuilder.DropColumn(
                name: "RechargeResumeAt",
                table: "digital_wallets");
        }
    }
}
