using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowOvernightLiveSupportSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_live_support_schedule_time",
                table: "live_support_schedule_windows");

            migrationBuilder.AddCheckConstraint(
                name: "CK_live_support_schedule_time",
                table: "live_support_schedule_windows",
                sql: "\"StartLocalTime\" <> \"EndLocalTime\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_live_support_schedule_time",
                table: "live_support_schedule_windows");

            migrationBuilder.AddCheckConstraint(
                name: "CK_live_support_schedule_time",
                table: "live_support_schedule_windows",
                sql: "\"StartLocalTime\" < \"EndLocalTime\"");
        }
    }
}
