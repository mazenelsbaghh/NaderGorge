using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LearningConcept",
                table: "question_bank_items",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LearningDifficulty",
                table: "question_bank_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LearningLessonId",
                table: "question_bank_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByQuestionId",
                table: "question_bank_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "learning_follow_ups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_follow_ups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_learning_follow_ups_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_learning_follow_ups_users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_learning_follow_ups_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_question_bank_items_LearningLessonId_LearningDifficulty",
                table: "question_bank_items",
                columns: new[] { "LearningLessonId", "LearningDifficulty" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_follow_ups_PackageId_StudentId_CreatedAt",
                table: "learning_follow_ups",
                columns: new[] { "PackageId", "StudentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_follow_ups_PerformedByUserId",
                table: "learning_follow_ups",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_follow_ups_StudentId",
                table: "learning_follow_ups",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_question_bank_items_lessons_LearningLessonId",
                table: "question_bank_items",
                column: "LearningLessonId",
                principalTable: "lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_question_bank_items_lessons_LearningLessonId",
                table: "question_bank_items");

            migrationBuilder.DropTable(
                name: "learning_follow_ups");

            migrationBuilder.DropIndex(
                name: "IX_question_bank_items_LearningLessonId_LearningDifficulty",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "LearningConcept",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "LearningDifficulty",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "LearningLessonId",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "SupersededByQuestionId",
                table: "question_bank_items");
        }
    }
}
