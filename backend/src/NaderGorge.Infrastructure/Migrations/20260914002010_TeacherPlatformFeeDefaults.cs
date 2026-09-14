using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TeacherPlatformFeeDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinancePreset",
                table: "teacher_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql("""
                UPDATE teacher_profiles SET "FinancePreset" = CASE
                    WHEN "Id" IN ('73cf9e05-d068-4a0c-b8e6-dcba16554417', 'ff2b0754-3dcd-4b33-85a9-f5869e0c2768') THEN 1
                    WHEN "Id" = '2a0e7d2f-1dd7-4af0-9974-c999489899b2' THEN 2 ELSE 0 END;

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException("Financial agreement history requires a reviewed forward correction; it cannot be rolled back by dropping the preset column.");
        }
    }
}
