using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityCommentModerationAndCriticalExamFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmittedText",
                table: "student_answers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "community_post_comments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "community_post_comments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "community_post_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "community_post_comments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_community_post_comments_ReviewedByUserId",
                table: "community_post_comments",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_post_comments_Status",
                table: "community_post_comments",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_community_post_comments_users_ReviewedByUserId",
                table: "community_post_comments",
                column: "ReviewedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_community_post_comments_users_ReviewedByUserId",
                table: "community_post_comments");

            migrationBuilder.DropIndex(
                name: "IX_community_post_comments_ReviewedByUserId",
                table: "community_post_comments");

            migrationBuilder.DropIndex(
                name: "IX_community_post_comments_Status",
                table: "community_post_comments");

            migrationBuilder.DropColumn(
                name: "SubmittedText",
                table: "student_answers");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "community_post_comments");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "community_post_comments");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "community_post_comments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "community_post_comments");
        }
    }
}
