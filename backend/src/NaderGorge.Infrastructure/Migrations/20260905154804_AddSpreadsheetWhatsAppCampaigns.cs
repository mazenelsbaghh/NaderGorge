using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpreadsheetWhatsAppCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "StudentUserId",
                table: "whatsapp_campaign_recipients",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM whatsapp_campaign_audit_events
                WHERE "CampaignId" IN (
                    SELECT DISTINCT "CampaignId" FROM whatsapp_campaign_recipients
                    WHERE "StudentUserId" IS NULL);
                DELETE FROM whatsapp_campaign_recipients WHERE "StudentUserId" IS NULL;
                DELETE FROM whatsapp_campaigns
                WHERE "AudienceFilterJson" -> 'contactRoles' = '["Spreadsheet"]'::jsonb;
                """);
            migrationBuilder.AlterColumn<Guid>(
                name: "StudentUserId",
                table: "whatsapp_campaign_recipients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
