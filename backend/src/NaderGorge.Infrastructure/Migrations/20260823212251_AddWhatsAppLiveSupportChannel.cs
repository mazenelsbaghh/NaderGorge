using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppLiveSupportChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_support_whatsapp_bindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppUserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LastInboundAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerServiceWindowExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_whatsapp_bindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_whatsapp_bindings_live_support_conversations_C~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_whatsapp_bindings_live_support_guest_sessions_~",
                        column: x => x.GuestSessionId,
                        principalTable: "live_support_guest_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_whatsapp_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSupportMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetaMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TemplateLanguage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TemplateParametersJson = table.Column<string>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_live_support_whatsapp_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_whatsapp_messages_live_support_conversations_C~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_whatsapp_messages_live_support_messages_LiveSu~",
                        column: x => x.LiveSupportMessageId,
                        principalTable: "live_support_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_whatsapp_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetaTemplateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ComponentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_whatsapp_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_bindings_ConversationId",
                table: "live_support_whatsapp_bindings",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_bindings_GuestSessionId",
                table: "live_support_whatsapp_bindings",
                column: "GuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_bindings_PhoneNumber_LastInboundAt",
                table: "live_support_whatsapp_bindings",
                columns: new[] { "PhoneNumber", "LastInboundAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_bindings_WhatsAppUserId_LastInboundAt",
                table: "live_support_whatsapp_bindings",
                columns: new[] { "WhatsAppUserId", "LastInboundAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_messages_ConversationId_CreatedAt",
                table: "live_support_whatsapp_messages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_messages_LiveSupportMessageId",
                table: "live_support_whatsapp_messages",
                column: "LiveSupportMessageId",
                unique: true,
                filter: "\"LiveSupportMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_messages_MetaMessageId",
                table: "live_support_whatsapp_messages",
                column: "MetaMessageId",
                unique: true,
                filter: "\"MetaMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_messages_Status_NextAttemptAt",
                table: "live_support_whatsapp_messages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_templates_MetaTemplateId",
                table: "live_support_whatsapp_templates",
                column: "MetaTemplateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_templates_Name_Language",
                table: "live_support_whatsapp_templates",
                columns: new[] { "Name", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_whatsapp_templates_Status_LastSyncedAt",
                table: "live_support_whatsapp_templates",
                columns: new[] { "Status", "LastSyncedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_support_whatsapp_bindings");

            migrationBuilder.DropTable(
                name: "live_support_whatsapp_messages");

            migrationBuilder.DropTable(
                name: "live_support_whatsapp_templates");
        }
    }
}
