using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoTypeCodeGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VideoTypeId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VideoTypeId",
                table: "code_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.DropCheckConstraint(
                name: "CK_student_access_grants_target_shape",
                table: "student_access_grants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_student_access_grants_target_shape",
                table: "student_access_grants",
                sql: "(\"GrantType\" = 0 AND \"PackageId\" IS NOT NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 1 AND \"TermId\" IS NOT NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 2 AND \"ContentSectionId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 3 AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 4 AND (\"LessonVideoId\" IS NOT NULL OR \"VideoTypeId\" IS NOT NULL) AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 5 AND \"ExamId\" IS NOT NULL AND \"LessonVideoId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_code_groups_VideoTypeId",
                table: "code_groups",
                column: "VideoTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_video_type_scope",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "VideoTypeId", "PackageId", "TermId", "ContentSectionId", "LessonId" },
                filter: "\"IsActive\" = TRUE AND \"GrantType\" = 4 AND \"VideoTypeId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_code_groups_VideoTypeId",
                table: "code_groups");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_video_type_scope",
                table: "student_access_grants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_student_access_grants_target_shape",
                table: "student_access_grants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_student_access_grants_target_shape",
                table: "student_access_grants",
                sql: "(\"GrantType\" = 0 AND \"PackageId\" IS NOT NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 1 AND \"TermId\" IS NOT NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 2 AND \"ContentSectionId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 3 AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 4 AND \"LessonVideoId\" IS NOT NULL AND \"ExamId\" IS NULL) OR " +
                     "(\"GrantType\" = 5 AND \"ExamId\" IS NOT NULL AND \"LessonVideoId\" IS NULL)");

            migrationBuilder.DropColumn(
                name: "VideoTypeId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "VideoTypeId",
                table: "code_groups");
        }
    }
}
