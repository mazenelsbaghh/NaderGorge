using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBunnyHlsPlayerChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BunnyPlaybackMode",
                table: "lesson_videos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetBunnyPlaybackMode",
                table: "bunny_video_assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HlsCdnHostname",
                table: "bunny_stream_libraries",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "HlsTokenKeyCiphertext",
                table: "bunny_stream_libraries",
                type: "bytea",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "bunny_stream_libraries",
                keyColumn: "Id",
                keyValue: new Guid("a5d123ac-0b9f-4f69-9d15-740733000001"),
                columns: new[] { "HlsCdnHostname", "HlsTokenKeyCiphertext" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "bunny_stream_libraries",
                keyColumn: "Id",
                keyValue: new Guid("a5d123ac-0b9f-4f69-9d15-740737000002"),
                columns: new[] { "HlsCdnHostname", "HlsTokenKeyCiphertext" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "bunny_stream_libraries",
                keyColumn: "Id",
                keyValue: new Guid("a5d123ac-0b9f-4f69-9d15-740801000003"),
                columns: new[] { "HlsCdnHostname", "HlsTokenKeyCiphertext" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BunnyPlaybackMode",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "TargetBunnyPlaybackMode",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "HlsCdnHostname",
                table: "bunny_stream_libraries");

            migrationBuilder.DropColumn(
                name: "HlsTokenKeyCiphertext",
                table: "bunny_stream_libraries");
        }
    }
}
