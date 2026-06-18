using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackSessionProgressState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasRegisteredView",
                table: "VideoPlaybackSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperseded",
                table: "VideoPlaybackSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProgressAt",
                table: "VideoPlaybackSessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastProgressSequence",
                table: "VideoPlaybackSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_VideoPlaybackSessions_UserId_LessonVideoId_CreatedAt",
                table: "VideoPlaybackSessions",
                columns: new[] { "UserId", "LessonVideoId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoPlaybackSessions_UserId_LessonVideoId_CreatedAt",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "HasRegisteredView",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "IsSuperseded",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "LastProgressAt",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "LastProgressSequence",
                table: "VideoPlaybackSessions");
        }
    }
}
