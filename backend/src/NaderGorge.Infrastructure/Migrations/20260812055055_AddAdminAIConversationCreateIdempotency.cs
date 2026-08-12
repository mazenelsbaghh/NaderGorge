using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAIConversationCreateIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreateIdempotencyDigest",
                table: "admin_ai_conversations",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatePayloadHash",
                table: "admin_ai_conversations",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_conversations_OwnerAdminUserId_CreateIdempotencyDi~",
                table: "admin_ai_conversations",
                columns: new[] { "OwnerAdminUserId", "CreateIdempotencyDigest" },
                unique: true,
                filter: "\"CreateIdempotencyDigest\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_admin_ai_conversations_OwnerAdminUserId_CreateIdempotencyDi~",
                table: "admin_ai_conversations");

            migrationBuilder.DropColumn(
                name: "CreateIdempotencyDigest",
                table: "admin_ai_conversations");

            migrationBuilder.DropColumn(
                name: "CreatePayloadHash",
                table: "admin_ai_conversations");
        }
    }
}
