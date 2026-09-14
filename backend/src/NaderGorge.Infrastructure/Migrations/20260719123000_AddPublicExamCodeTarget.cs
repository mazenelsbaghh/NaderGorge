using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

public partial class AddPublicExamCodeTarget : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PublicExamProductId",
            table: "code_groups",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_code_groups_PublicExamProductId",
            table: "code_groups",
            column: "PublicExamProductId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_code_groups_PublicExamProductId",
            table: "code_groups");

        migrationBuilder.DropColumn(
            name: "PublicExamProductId",
            table: "code_groups");
    }
}
