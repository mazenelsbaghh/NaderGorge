using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchApprovalOperationIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationId",
                table: "video_overrides",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WatchRequestId",
                table: "video_overrides",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_overrides_OperationId",
                table: "video_overrides",
                column: "OperationId",
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_video_overrides_WatchRequestId",
                table: "video_overrides",
                column: "WatchRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_video_overrides_OperationId",
                table: "video_overrides");

            migrationBuilder.DropIndex(
                name: "IX_video_overrides_WatchRequestId",
                table: "video_overrides");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "video_overrides");

            migrationBuilder.DropColumn(
                name: "WatchRequestId",
                table: "video_overrides");
        }
    }
}
