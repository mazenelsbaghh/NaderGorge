using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706111500_AddNotificationEventAcademicScope")]
    public partial class AddNotificationEventAcademicScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicScopeOwnerId",
                table: "notification_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcademicScopeOwnerType",
                table: "notification_events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_events_AcademicScopeOwnerType_AcademicScopeOwnerId",
                table: "notification_events",
                columns: new[] { "AcademicScopeOwnerType", "AcademicScopeOwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notification_events_AcademicScopeOwnerType_AcademicScopeOwnerId",
                table: "notification_events");

            migrationBuilder.DropColumn(
                name: "AcademicScopeOwnerId",
                table: "notification_events");

            migrationBuilder.DropColumn(
                name: "AcademicScopeOwnerType",
                table: "notification_events");
        }
    }
}
