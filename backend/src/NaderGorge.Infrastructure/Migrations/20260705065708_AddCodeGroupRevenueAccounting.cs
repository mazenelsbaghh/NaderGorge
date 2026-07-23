using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeGroupRevenueAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccountingRecordedAt",
                table: "code_groups",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountingTiming",
                table: "code_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RevenueAllocationMode",
                table: "code_groups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RevenueAllocationValue",
                table: "code_groups",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevenueOwner",
                table: "code_groups",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountingRecordedAt",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "AccountingTiming",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "RevenueAllocationMode",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "RevenueAllocationValue",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "RevenueOwner",
                table: "code_groups");
        }
    }
}
