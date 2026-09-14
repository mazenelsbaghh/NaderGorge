using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityCommentReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "community_post_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_post_comments_ParentCommentId",
                table: "community_post_comments",
                column: "ParentCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_community_post_comments_parent",
                table: "community_post_comments",
                column: "ParentCommentId",
                principalTable: "community_post_comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_community_post_comments_parent",
                table: "community_post_comments");

            migrationBuilder.DropIndex(
                name: "IX_community_post_comments_ParentCommentId",
                table: "community_post_comments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "community_post_comments");
        }
    }
}
