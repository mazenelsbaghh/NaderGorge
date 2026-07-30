using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCommunity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_posts_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_community_posts_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "community_post_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_post_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_post_comments_community_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_post_comments_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "community_post_likes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_post_likes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_post_likes_community_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_post_likes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_post_comments_AuthorUserId",
                table: "community_post_comments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_post_comments_CreatedAt",
                table: "community_post_comments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_community_post_comments_PostId",
                table: "community_post_comments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_community_post_likes_PostId",
                table: "community_post_likes",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_community_post_likes_PostId_UserId",
                table: "community_post_likes",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_post_likes_UserId",
                table: "community_post_likes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_AuthorUserId",
                table: "community_posts",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_CreatedAt",
                table: "community_posts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_ReviewedByUserId",
                table: "community_posts",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_Status",
                table: "community_posts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_post_comments");

            migrationBuilder.DropTable(
                name: "community_post_likes");

            migrationBuilder.DropTable(
                name: "community_posts");
        }
    }
}
