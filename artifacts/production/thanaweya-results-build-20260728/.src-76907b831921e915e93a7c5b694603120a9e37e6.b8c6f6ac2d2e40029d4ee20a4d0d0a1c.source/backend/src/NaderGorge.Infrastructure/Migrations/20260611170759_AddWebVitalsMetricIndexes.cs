using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebVitalsMetricIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_web_vitals_metrics_CreatedAt",
                table: "web_vitals_metrics",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_web_vitals_metrics_MetricName",
                table: "web_vitals_metrics",
                column: "MetricName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_web_vitals_metrics_CreatedAt",
                table: "web_vitals_metrics");

            migrationBuilder.DropIndex(
                name: "IX_web_vitals_metrics_MetricName",
                table: "web_vitals_metrics");
        }
    }
}
