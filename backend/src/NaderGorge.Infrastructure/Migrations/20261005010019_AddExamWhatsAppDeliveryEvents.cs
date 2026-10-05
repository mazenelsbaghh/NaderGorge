using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamWhatsAppDeliveryEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamWhatsAppDeliveryEvents",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BusinessAccountId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MessageId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EventUnixTime = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCode = table.Column<int>(type: "integer", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamWhatsAppDeliveryEvents", x => x.Fingerprint);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamWhatsAppDeliveryEvents_BusinessAccountId_MessageId_Even~",
                table: "ExamWhatsAppDeliveryEvents",
                columns: new[] { "BusinessAccountId", "MessageId", "EventUnixTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamWhatsAppDeliveryEvents");
        }
    }
}
