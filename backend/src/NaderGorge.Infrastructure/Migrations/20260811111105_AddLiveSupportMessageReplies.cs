using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSupportMessageReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "live_support_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messages_ReplyToMessageId",
                table: "live_support_messages",
                column: "ReplyToMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_live_support_messages_live_support_messages_ReplyToMessageId",
                table: "live_support_messages",
                column: "ReplyToMessageId",
                principalTable: "live_support_messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_live_support_messages_live_support_messages_ReplyToMessageId",
                table: "live_support_messages");

            migrationBuilder.DropIndex(
                name: "IX_live_support_messages_ReplyToMessageId",
                table: "live_support_messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "live_support_messages");
        }
    }
}
