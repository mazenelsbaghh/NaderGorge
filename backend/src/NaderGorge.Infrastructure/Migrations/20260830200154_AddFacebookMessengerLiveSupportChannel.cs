using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacebookMessengerLiveSupportChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "live_support_guest_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsAI",
                table: "live_support_conversations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "live_support_messenger_bindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PageName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SenderPsid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    LastInboundAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReplyWindowExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_messenger_bindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_messenger_bindings_live_support_conversations_~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_messenger_bindings_live_support_guest_sessions~",
                        column: x => x.GuestSessionId,
                        principalTable: "live_support_guest_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_messenger_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSupportMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    PageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SenderPsid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProviderTimestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_messenger_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_messenger_messages_live_support_conversations_~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_messenger_messages_live_support_messages_LiveS~",
                        column: x => x.LiveSupportMessageId,
                        principalTable: "live_support_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_messenger_webhook_inbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(384)", maxLength: 384, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_messenger_webhook_inbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_bindings_ConversationId",
                table: "live_support_messenger_bindings",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_bindings_GuestSessionId",
                table: "live_support_messenger_bindings",
                column: "GuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_bindings_PageId_SenderPsid",
                table: "live_support_messenger_bindings",
                columns: new[] { "PageId", "SenderPsid" },
                unique: true,
                filter: "\"IsOpen\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_bindings_PageId_SenderPsid_LastInbou~",
                table: "live_support_messenger_bindings",
                columns: new[] { "PageId", "SenderPsid", "LastInboundAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_messages_ConversationId_CreatedAt",
                table: "live_support_messenger_messages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_messages_LiveSupportMessageId",
                table: "live_support_messenger_messages",
                column: "LiveSupportMessageId",
                unique: true,
                filter: "\"LiveSupportMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_messages_PageId_ProviderMessageId",
                table: "live_support_messenger_messages",
                columns: new[] { "PageId", "ProviderMessageId" },
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_messages_Status_NextAttemptAt",
                table: "live_support_messenger_messages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_webhook_inbox_PageId_DeduplicationKey",
                table: "live_support_messenger_webhook_inbox",
                columns: new[] { "PageId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_webhook_inbox_Status_NextAttemptAt",
                table: "live_support_messenger_webhook_inbox",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_support_messenger_bindings");

            migrationBuilder.DropTable(
                name: "live_support_messenger_messages");

            migrationBuilder.DropTable(
                name: "live_support_messenger_webhook_inbox");

            migrationBuilder.DropColumn(
                name: "AllowsAI",
                table: "live_support_conversations");

            migrationBuilder.Sql(
                "UPDATE live_support_guest_sessions SET \"PhoneNumber\" = '' WHERE \"PhoneNumber\" IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "live_support_guest_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
