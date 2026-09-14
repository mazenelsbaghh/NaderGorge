using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706180000_AddSharedPackageItemPrices")]
    public partial class AddSharedPackageItemPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "shared_teacher_package_items",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                WITH group_counts AS (
                    SELECT
                        "SharedTeacherPackageId",
                        COUNT(DISTINCT COALESCE("SubjectId", "TeacherId"))::numeric AS "GroupCount"
                    FROM shared_teacher_package_items
                    GROUP BY "SharedTeacherPackageId"
                ),
                priced_items AS (
                    SELECT
                        item."Id",
                        ROUND((shared_package."Price" / NULLIF(group_counts."GroupCount", 0))::numeric, 4) AS "ItemPrice"
                    FROM shared_teacher_package_items item
                    INNER JOIN shared_teacher_packages shared_package
                        ON shared_package."Id" = item."SharedTeacherPackageId"
                    INNER JOIN group_counts
                        ON group_counts."SharedTeacherPackageId" = item."SharedTeacherPackageId"
                )
                UPDATE shared_teacher_package_items item
                SET "Price" = priced_items."ItemPrice"
                FROM priced_items
                WHERE item."Id" = priced_items."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "shared_teacher_package_items");
        }
    }
}
