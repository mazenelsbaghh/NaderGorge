using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTermAndSectionGiftTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_gift_issuances_target",
                table: "gift_issuances");

            migrationBuilder.AddColumn<Guid>(
                name: "ContentSectionId",
                table: "gift_issuances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TermId",
                table: "gift_issuances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_ContentSectionId",
                table: "gift_issuances",
                column: "ContentSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_TermId",
                table: "gift_issuances",
                column: "TermId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gift_issuances_target",
                table: "gift_issuances",
                sql: "(\"TargetType\" = 0 AND \"PackageId\" IS NOT NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 1 AND \"PackageId\" IS NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 2 AND \"PackageId\" IS NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NOT NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 3 AND \"PackageId\" IS NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NOT NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 4 AND \"PackageId\" IS NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" > 0) OR (\"TargetType\" = 5 AND \"PackageId\" IS NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NOT NULL AND \"Amount\" > 0) OR (\"TargetType\" = 6 AND \"PackageId\" IS NULL AND \"TermId\" IS NOT NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 7 AND \"PackageId\" IS NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_gift_issuances_content_sections_ContentSectionId",
                table: "gift_issuances",
                column: "ContentSectionId",
                principalTable: "content_sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_gift_issuances_terms_TermId",
                table: "gift_issuances",
                column: "TermId",
                principalTable: "terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gift_issuances_content_sections_ContentSectionId",
                table: "gift_issuances");

            migrationBuilder.DropForeignKey(
                name: "FK_gift_issuances_terms_TermId",
                table: "gift_issuances");

            migrationBuilder.DropIndex(
                name: "IX_gift_issuances_ContentSectionId",
                table: "gift_issuances");

            migrationBuilder.DropIndex(
                name: "IX_gift_issuances_TermId",
                table: "gift_issuances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gift_issuances_target",
                table: "gift_issuances");

            migrationBuilder.DropColumn(
                name: "ContentSectionId",
                table: "gift_issuances");

            migrationBuilder.DropColumn(
                name: "TermId",
                table: "gift_issuances");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gift_issuances_target",
                table: "gift_issuances",
                sql: "(\"TargetType\" = 0 AND \"PackageId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 1 AND \"PackageId\" IS NULL AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 2 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NOT NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 3 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NOT NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 4 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" > 0) OR (\"TargetType\" = 5 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NOT NULL AND \"Amount\" > 0)");
        }
    }
}
