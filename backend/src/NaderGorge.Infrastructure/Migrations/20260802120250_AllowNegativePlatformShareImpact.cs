using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowNegativePlatformShareImpact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_financial_effect_amounts",
                table: "sales_financial_effects");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_financial_effect_amounts",
                table: "sales_financial_effects",
                sql: "\"GrossAmount\" >= 0 AND \"CouponDiscountAmount\" >= 0 AND \"PrintableCodeDiscountAmount\" >= 0 AND \"PromotionalAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"TeacherShareImpact\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_financial_effect_amounts",
                table: "sales_financial_effects");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_financial_effect_amounts",
                table: "sales_financial_effects",
                sql: "\"GrossAmount\" >= 0 AND \"CouponDiscountAmount\" >= 0 AND \"PrintableCodeDiscountAmount\" >= 0 AND \"PromotionalAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"TeacherShareImpact\" >= 0 AND \"PlatformShareImpact\" >= 0");
        }
    }
}
