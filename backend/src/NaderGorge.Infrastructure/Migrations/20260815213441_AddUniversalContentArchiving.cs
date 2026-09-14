using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalContentArchiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "terms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "terms",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "terms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "packages",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "lessons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "lessons",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "lessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "lesson_videos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "lesson_videos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "lesson_videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "lesson_resources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "lesson_resources",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "lesson_resources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "homeworks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "homeworks",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "homeworks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "exams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "exams",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "exams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "content_sections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "content_sections",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "content_sections",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "terms");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "terms");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "terms");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "lesson_resources");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "lesson_resources");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "lesson_resources");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "content_sections");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "content_sections");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "content_sections");
        }
    }
}
