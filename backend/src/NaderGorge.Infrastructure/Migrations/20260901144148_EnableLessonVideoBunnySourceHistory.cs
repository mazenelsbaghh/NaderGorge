using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableLessonVideoBunnySourceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bunny_video_assets_LessonVideoId",
                table: "bunny_video_assets");

            migrationBuilder.AddColumn<Guid>(
                name: "BunnyStreamLibraryRecordId",
                table: "bunny_video_assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAtUtc",
                table: "bunny_video_assets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetiredByUserId",
                table: "bunny_video_assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceState",
                table: "bunny_video_assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TargetIsActive",
                table: "bunny_video_assets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetMaxWatchCount",
                table: "bunny_video_assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetOrder",
                table: "bunny_video_assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetVideoTypeId",
                table: "bunny_video_assets",
                type: "uuid",
                nullable: true);

            // Preserve each existing managed asset's library independently of the
            // logical lesson video before later edits can point that video at a
            // different provider or library.
            migrationBuilder.Sql(
                """
                UPDATE "bunny_video_assets" AS asset
                SET "BunnyStreamLibraryRecordId" = video."BunnyStreamLibraryId"
                FROM "lesson_videos" AS video
                WHERE video."Id" = asset."LessonVideoId"
                  AND video."BunnyStreamLibraryId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_BunnyStreamLibraryRecordId",
                table: "bunny_video_assets",
                column: "BunnyStreamLibraryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_CurrentLessonVideoId",
                table: "bunny_video_assets",
                column: "LessonVideoId",
                unique: true,
                filter: "\"SourceState\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_PendingLessonVideoId",
                table: "bunny_video_assets",
                column: "LessonVideoId",
                unique: true,
                filter: "\"SourceState\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_bunny_video_assets_bunny_stream_libraries_BunnyStreamLibrar~",
                table: "bunny_video_assets",
                column: "BunnyStreamLibraryRecordId",
                principalTable: "bunny_stream_libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bunny_video_assets_bunny_stream_libraries_BunnyStreamLibrar~",
                table: "bunny_video_assets");

            migrationBuilder.DropIndex(
                name: "IX_bunny_video_assets_BunnyStreamLibraryRecordId",
                table: "bunny_video_assets");

            migrationBuilder.DropIndex(
                name: "IX_bunny_video_assets_CurrentLessonVideoId",
                table: "bunny_video_assets");

            migrationBuilder.DropIndex(
                name: "IX_bunny_video_assets_PendingLessonVideoId",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "BunnyStreamLibraryRecordId",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "RetiredAtUtc",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "RetiredByUserId",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "SourceState",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "TargetIsActive",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "TargetMaxWatchCount",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "TargetOrder",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "TargetVideoTypeId",
                table: "bunny_video_assets");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_LessonVideoId",
                table: "bunny_video_assets",
                column: "LessonVideoId",
                unique: true);
        }
    }
}
