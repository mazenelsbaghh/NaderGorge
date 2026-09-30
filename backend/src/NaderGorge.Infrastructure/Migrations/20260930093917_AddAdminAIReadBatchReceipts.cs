using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAIReadBatchReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_ai_read_batch_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchKeyDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RequestDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ProtectedResponse = table.Column<byte[]>(type: "bytea", nullable: false),
                    ResponseHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResponseTurnVersion = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_read_batch_receipts", x => x.Id);
                    table.CheckConstraint("ck_admin_ai_read_batch_receipt_version", "\"ResponseTurnVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_admin_ai_read_batch_receipts_admin_ai_turn_steps_TurnStepId",
                        column: x => x.TurnStepId,
                        principalTable: "admin_ai_turn_steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_read_batch_receipts_admin_ai_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "admin_ai_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_read_batch_receipts_ExpiresAt",
                table: "admin_ai_read_batch_receipts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_read_batch_receipts_TurnId_BatchKeyDigest",
                table: "admin_ai_read_batch_receipts",
                columns: new[] { "TurnId", "BatchKeyDigest" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_read_batch_receipts_TurnStepId",
                table: "admin_ai_read_batch_receipts",
                column: "TurnStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_ai_read_batch_receipts");
        }
    }
}
