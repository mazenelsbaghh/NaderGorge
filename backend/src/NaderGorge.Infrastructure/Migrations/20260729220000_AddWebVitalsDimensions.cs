using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260729220000_AddWebVitalsDimensions")]
public partial class AddWebVitalsDimensions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("SET LOCAL lock_timeout = '5s';");

        migrationBuilder.AddColumn<string>(
            name: "ConnectionClass",
            table: "web_vitals_metrics",
            type: "character varying(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "unknown");
        migrationBuilder.AddColumn<string>(
            name: "CorrelationId",
            table: "web_vitals_metrics",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DeviceClass",
            table: "web_vitals_metrics",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "unknown");
        migrationBuilder.AddColumn<string>(
            name: "MetricId",
            table: "web_vitals_metrics",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "legacy");
        migrationBuilder.AddColumn<string>(
            name: "NavigationType",
            table: "web_vitals_metrics",
            type: "character varying(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "unknown");
        migrationBuilder.AddColumn<string>(
            name: "ReleaseId",
            table: "web_vitals_metrics",
            type: "character varying(96)",
            maxLength: 96,
            nullable: false,
            defaultValue: "legacy");
        migrationBuilder.AddColumn<string>(
            name: "RouteTemplate",
            table: "web_vitals_metrics",
            type: "character varying(180)",
            maxLength: 180,
            nullable: false,
            defaultValue: "/unknown");
        migrationBuilder.AddColumn<string>(
            name: "Surface",
            table: "web_vitals_metrics",
            type: "character varying(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "unknown");

        migrationBuilder.Sql("""
            SET lock_timeout = '5s';
            SET statement_timeout = '30min';
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            DROP INDEX CONCURRENTLY IF EXISTS
                "IX_web_vitals_metrics_ReleaseId_RouteTemplate_Surface_DeviceClass_MetricName_CreatedAt";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS
                "IX_web_vitals_metrics_ReleaseId_RouteTemplate_Surface_DeviceClass_MetricName_CreatedAt"
                ON web_vitals_metrics
                    ("ReleaseId", "RouteTemplate", "Surface", "DeviceClass",
                     "MetricName", "CreatedAt");
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            RESET statement_timeout;
            RESET lock_timeout;
            """, suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX CONCURRENTLY IF EXISTS
                "IX_web_vitals_metrics_ReleaseId_RouteTemplate_Surface_DeviceClass_MetricName_CreatedAt";
            """, suppressTransaction: true);
        migrationBuilder.DropColumn(name: "ConnectionClass", table: "web_vitals_metrics");
        migrationBuilder.DropColumn(name: "CorrelationId", table: "web_vitals_metrics");
        migrationBuilder.DropColumn(name: "DeviceClass", table: "web_vitals_metrics");
        migrationBuilder.DropColumn(name: "MetricId", table: "web_vitals_metrics");
        migrationBuilder.DropColumn(name: "NavigationType", table: "web_vitals_metrics");
        migrationBuilder.DropColumn(name: "ReleaseId", table: "web_vitals_metrics");
        migrationBuilder.DropColumn(name: "RouteTemplate", table: "web_vitals_metrics");
        migrationBuilder.DropColumn(name: "Surface", table: "web_vitals_metrics");
    }
}
