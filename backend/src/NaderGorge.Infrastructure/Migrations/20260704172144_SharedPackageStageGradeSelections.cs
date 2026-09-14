using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SharedPackageStageGradeSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EducationStage",
                table: "shared_teacher_packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GradeLevel",
                table: "shared_teacher_packages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_packages_EducationStage_GradeLevel_IsPublish~",
                table: "shared_teacher_packages",
                columns: new[] { "EducationStage", "GradeLevel", "IsPublished" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shared_teacher_packages_EducationStage_GradeLevel_IsPublish~",
                table: "shared_teacher_packages");

            migrationBuilder.DropColumn(
                name: "EducationStage",
                table: "shared_teacher_packages");

            migrationBuilder.DropColumn(
                name: "GradeLevel",
                table: "shared_teacher_packages");
        }
    }
}
