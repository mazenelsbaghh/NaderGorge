using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260728210000_AddThanaweyaResults")]
public partial class AddThanaweyaResults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS thanaweya_results (
                seating_no varchar(20) PRIMARY KEY,
                arabic_name text NOT NULL,
                total_degree numeric(7,2) NULL,
                student_case_desc text NOT NULL DEFAULT ''
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS thanaweya_results;");
}
