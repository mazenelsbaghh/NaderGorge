using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentIdentityAndVideoTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternalCode",
                table: "lessons",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalCode",
                table: "lesson_videos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VideoTypeId",
                table: "lesson_videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalCode",
                table: "exams",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "video_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_types", x => x.Id);
                });

            var seededAt = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "video_types",
                columns: new[] { "Id", "Name", "NormalizedName", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("15100000-0000-0000-0000-000000000001"), "شرح", "شرح", 10, true, seededAt, null },
                    { new Guid("15100000-0000-0000-0000-000000000002"), "واجب", "واجب", 20, true, seededAt, null },
                    { new Guid("15100000-0000-0000-0000-000000000003"), "مراجعة", "مراجعة", 30, true, seededAt, null },
                    { new Guid("15100000-0000-0000-0000-000000000004"), "امتحان", "امتحان", 40, true, seededAt, null },
                    { new Guid("15100000-0000-0000-0000-000000000005"), "غير مصنف", "غير مصنف", 999, false, seededAt, null }
                });

            migrationBuilder.Sql("""
                UPDATE lessons
                SET "InternalCode" = 'LES-' || replace("Id"::text, '-', '');

                UPDATE lesson_videos
                SET "InternalCode" = 'VID-' || replace("Id"::text, '-', ''),
                    "VideoTypeId" = CASE
                        WHEN lower(trim(coalesce("VideoTag", ''))) IN ('شرح', 'explanation', 'lesson')
                            THEN '15100000-0000-0000-0000-000000000001'::uuid
                        WHEN lower(trim(coalesce("VideoTag", ''))) IN ('واجب', 'homework', 'assignment')
                            THEN '15100000-0000-0000-0000-000000000002'::uuid
                        WHEN lower(trim(coalesce("VideoTag", ''))) IN ('مراجعة', 'review', 'revision')
                            THEN '15100000-0000-0000-0000-000000000003'::uuid
                        WHEN lower(trim(coalesce("VideoTag", ''))) IN ('امتحان', 'exam', 'quiz')
                            THEN '15100000-0000-0000-0000-000000000004'::uuid
                        ELSE '15100000-0000-0000-0000-000000000005'::uuid
                    END;

                UPDATE exams
                SET "InternalCode" = 'EXM-' || replace("Id"::text, '-', '');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "InternalCode",
                table: "lessons",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InternalCode",
                table: "lesson_videos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VideoTypeId",
                table: "lesson_videos",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InternalCode",
                table: "exams",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lessons_InternalCode",
                table: "lessons",
                column: "InternalCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_videos_InternalCode",
                table: "lesson_videos",
                column: "InternalCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_videos_VideoTypeId",
                table: "lesson_videos",
                column: "VideoTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_exams_InternalCode",
                table: "exams",
                column: "InternalCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_types_NormalizedName",
                table: "video_types",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_types_SortOrder_Name",
                table: "video_types",
                columns: new[] { "SortOrder", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_videos_video_types_VideoTypeId",
                table: "lesson_videos",
                column: "VideoTypeId",
                principalTable: "video_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lesson_videos_video_types_VideoTypeId",
                table: "lesson_videos");

            migrationBuilder.DropTable(
                name: "video_types");

            migrationBuilder.DropIndex(
                name: "IX_lessons_InternalCode",
                table: "lessons");

            migrationBuilder.DropIndex(
                name: "IX_lesson_videos_InternalCode",
                table: "lesson_videos");

            migrationBuilder.DropIndex(
                name: "IX_lesson_videos_VideoTypeId",
                table: "lesson_videos");

            migrationBuilder.DropIndex(
                name: "IX_exams_InternalCode",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "InternalCode",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "InternalCode",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "VideoTypeId",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "InternalCode",
                table: "exams");
        }
    }
}
