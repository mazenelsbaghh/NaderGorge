using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveQuestionDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.DropColumn(
                name: "TimePerQuestionSeconds",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "exam_questions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AddColumn<int>(
                name: "TimePerQuestionSeconds",
                table: "exams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "exam_questions",
                type: "integer",
                nullable: true);
        }
    }
}
