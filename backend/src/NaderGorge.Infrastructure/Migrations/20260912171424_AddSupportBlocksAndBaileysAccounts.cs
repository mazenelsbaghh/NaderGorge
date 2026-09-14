using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportBlocksAndBaileysAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "live_support_whatsapp_bindings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "live_support_contact_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuestSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BlockedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnblockedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UnblockedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_contact_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_contact_blocks_live_support_conversations_Conv~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_whatsapp_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InstanceName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_whatsapp_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "live_support_baileys_auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Ciphertext = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_baileys_auth", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_baileys_auth_live_support_whatsapp_accounts_Ac~",
                        column: x => x.AccountId,
                        principalTable: "live_support_whatsapp_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_baileys_callbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ciphertext = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_baileys_callbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_baileys_callbacks_live_support_whatsapp_accoun~",
                        column: x => x.AccountId,
                        principalTable: "live_support_whatsapp_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_block_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DesiredBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NoticeStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_block_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_block_deliveries_live_support_contact_blocks_B~",
                        column: x => x.BlockId,
                        principalTable: "live_support_contact_blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_block_deliveries_live_support_whatsapp_account~",
                        column: x => x.AccountId,
                        principalTable: "live_support_whatsapp_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_bindings_AccountId",
                table: "live_support_whatsapp_bindings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_baileys_auth_AccountId_Key",
                table: "live_support_baileys_auth",
                columns: new[] { "AccountId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_baileys_callbacks_AccountId",
                table: "live_support_baileys_callbacks",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_baileys_callbacks_NextAttemptAt",
                table: "live_support_baileys_callbacks",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_block_deliveries_AccountId",
                table: "live_support_block_deliveries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_block_deliveries_BlockId",
                table: "live_support_block_deliveries",
                column: "BlockId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_block_deliveries_Status_CreatedAt",
                table: "live_support_block_deliveries",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_contact_blocks_ConversationId",
                table: "live_support_contact_blocks",
                column: "ConversationId",
                unique: true,
                filter: "\"UnblockedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_contact_blocks_GuestSessionId_UnblockedAt",
                table: "live_support_contact_blocks",
                columns: new[] { "GuestSessionId", "UnblockedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_contact_blocks_PhoneNumber_UnblockedAt",
                table: "live_support_contact_blocks",
                columns: new[] { "PhoneNumber", "UnblockedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_contact_blocks_StudentUserId_UnblockedAt",
                table: "live_support_contact_blocks",
                columns: new[] { "StudentUserId", "UnblockedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_accounts_InstanceName",
                table: "live_support_whatsapp_accounts",
                column: "InstanceName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_live_support_whatsapp_bindings_live_support_whatsapp_accoun~",
                table: "live_support_whatsapp_bindings",
                column: "AccountId",
                principalTable: "live_support_whatsapp_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_live_support_whatsapp_bindings_live_support_whatsapp_accoun~",
                table: "live_support_whatsapp_bindings");

            migrationBuilder.DropTable(
                name: "live_support_baileys_auth");

            migrationBuilder.DropTable(
                name: "live_support_baileys_callbacks");

            migrationBuilder.DropTable(
                name: "live_support_block_deliveries");

            migrationBuilder.DropTable(
                name: "live_support_contact_blocks");

            migrationBuilder.DropTable(
                name: "live_support_whatsapp_accounts");

            migrationBuilder.DropIndex(
                name: "IX_live_support_whatsapp_bindings_AccountId",
                table: "live_support_whatsapp_bindings");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "live_support_whatsapp_bindings");
        }
    }
}
