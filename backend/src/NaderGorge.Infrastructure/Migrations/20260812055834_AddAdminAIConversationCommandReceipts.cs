using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAIConversationCommandReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_ai_conversation_command_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResponseTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ResponseStatus = table.Column<int>(type: "integer", nullable: false),
                    ResponseLastActivityAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResponseVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_conversation_command_receipts", x => x.Id);
                    table.CheckConstraint("ck_admin_ai_conversation_receipt_version", "\"ResponseVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_admin_ai_conversation_command_receipts_admin_ai_conversatio~",
                        column: x => x.ConversationId,
                        principalTable: "admin_ai_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_conversation_command_receipts_users_OwnerAdminUser~",
                        column: x => x.OwnerAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_conversation_command_receipts_ConversationId",
                table: "admin_ai_conversation_command_receipts",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_conversation_command_receipts_OwnerAdminUserId_Ide~",
                table: "admin_ai_conversation_command_receipts",
                columns: new[] { "OwnerAdminUserId", "IdempotencyDigest" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_ai_conversation_command_receipts");
        }
    }
}
