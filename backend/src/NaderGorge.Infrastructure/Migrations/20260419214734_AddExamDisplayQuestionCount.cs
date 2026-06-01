using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamDisplayQuestionCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayQuestionCount",
                table: "exams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPoll",
                table: "community_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "community_post_poll_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_post_poll_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_post_poll_options_community_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_post_poll_votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_post_poll_votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_post_poll_votes_community_post_poll_options_PollO~",
                        column: x => x.PollOptionId,
                        principalTable: "community_post_poll_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_community_post_poll_votes_community_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_post_poll_votes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_post_poll_options_PostId",
                table: "community_post_poll_options",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_community_post_poll_votes_PollOptionId",
                table: "community_post_poll_votes",
                column: "PollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_community_post_poll_votes_PostId_UserId",
                table: "community_post_poll_votes",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_post_poll_votes_UserId",
                table: "community_post_poll_votes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_post_poll_votes");

            migrationBuilder.DropTable(
                name: "community_post_poll_options");

            migrationBuilder.DropColumn(
                name: "DisplayQuestionCount",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "IsPoll",
                table: "community_posts");
        }
    }
}
