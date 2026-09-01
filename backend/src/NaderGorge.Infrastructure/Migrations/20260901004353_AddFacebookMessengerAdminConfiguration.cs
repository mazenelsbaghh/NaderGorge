using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacebookMessengerAdminConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_support_messenger_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AppId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApiVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AppSecretCiphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    VerifyTokenCiphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    VerifyTokenRotatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_messenger_configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "live_support_messenger_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PageAccessTokenCiphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HumanAgentEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenValid = table.Column<bool>(type: "boolean", nullable: true),
                    IsSubscribed = table.Column<bool>(type: "boolean", nullable: true),
                    LastCredentialCheckAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSubscriptionCheckAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_messenger_pages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_configurations_ConfigurationKey",
                table: "live_support_messenger_configurations",
                column: "ConfigurationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_pages_IsEnabled_ConnectionStatus",
                table: "live_support_messenger_pages",
                columns: new[] { "IsEnabled", "ConnectionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messenger_pages_PageId",
                table: "live_support_messenger_pages",
                column: "PageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_support_messenger_configurations");

            migrationBuilder.DropTable(
                name: "live_support_messenger_pages");
        }
    }
}
