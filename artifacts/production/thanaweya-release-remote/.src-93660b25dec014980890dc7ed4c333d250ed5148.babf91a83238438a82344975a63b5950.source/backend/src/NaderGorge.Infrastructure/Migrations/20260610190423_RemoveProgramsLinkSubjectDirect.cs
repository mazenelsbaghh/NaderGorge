using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProgramsLinkSubjectDirect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_packages_programs_ProgramId",
                table: "packages");

            migrationBuilder.RenameColumn(
                name: "ProgramId",
                table: "packages",
                newName: "SubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_packages_ProgramId",
                table: "packages",
                newName: "IX_packages_SubjectId");

            migrationBuilder.AddColumn<string>(
                name: "TargetGrade",
                table: "packages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "All");

            // Custom migration SQL: Update SubjectId and TargetGrade from programs table
            migrationBuilder.Sql(@"
                UPDATE packages p
                SET ""TargetGrade"" = pr.""TargetGrade"",
                    ""SubjectId"" = pr.""SubjectId""
                FROM programs pr
                WHERE p.""SubjectId"" = pr.""Id"";
            ");

            migrationBuilder.DropTable(
                name: "programs");

            migrationBuilder.AddForeignKey(
                name: "FK_packages_subjects_SubjectId",
                table: "packages",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_packages_subjects_SubjectId",
                table: "packages");

            migrationBuilder.CreateTable(
                name: "programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetGrade = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_programs_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_programs_SubjectId",
                table: "programs",
                column: "SubjectId");

            // Custom migration SQL: Recreate programs and point packages back to them
            migrationBuilder.Sql(@"
                -- Insert distinct subjects/grades from packages as programs
                INSERT INTO programs (""Id"", ""SubjectId"", ""TargetGrade"", ""Name"", ""Description"", ""CreatedAt"")
                SELECT 
                    gen_random_uuid(), 
                    ""SubjectId"", 
                    ""TargetGrade"", 
                    'Program for ' || ""TargetGrade"", 
                    'Auto-generated on rollback', 
                    NOW()
                FROM (SELECT DISTINCT ""SubjectId"", ""TargetGrade"" FROM packages) as unique_packages;

                -- Update packages to point back to the new programs
                UPDATE packages p
                SET ""SubjectId"" = pr.""Id""
                FROM programs pr
                WHERE p.""SubjectId"" = pr.""SubjectId"" 
                  AND p.""TargetGrade"" = pr.""TargetGrade"";
            ");

            migrationBuilder.DropColumn(
                name: "TargetGrade",
                table: "packages");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                table: "packages",
                newName: "ProgramId");

            migrationBuilder.RenameIndex(
                name: "IX_packages_SubjectId",
                table: "packages",
                newName: "IX_packages_ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_packages_programs_ProgramId",
                table: "packages",
                column: "ProgramId",
                principalTable: "programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
