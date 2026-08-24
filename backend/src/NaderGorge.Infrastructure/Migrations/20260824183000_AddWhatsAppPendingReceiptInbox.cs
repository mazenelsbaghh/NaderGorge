using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

public partial class AddWhatsAppPendingReceiptInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "live_support_whatsapp_pending_receipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MetaMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ProviderTimestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_live_support_whatsapp_pending_receipts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_live_support_whatsapp_pending_receipts_CreatedAt",
            table: "live_support_whatsapp_pending_receipts",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_live_support_whatsapp_pending_receipts_MetaMessageId",
            table: "live_support_whatsapp_pending_receipts",
            column: "MetaMessageId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "live_support_whatsapp_pending_receipts");
    }
}
