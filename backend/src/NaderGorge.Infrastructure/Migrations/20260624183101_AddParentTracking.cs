using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasSeenTrackingCodePopup",
                table: "student_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentTrackingCode",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "parent_device_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_device_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parent_device_tokens_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_ParentTrackingCode",
                table: "student_profiles",
                column: "ParentTrackingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parent_device_tokens_StudentId",
                table: "parent_device_tokens",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parent_device_tokens");

            migrationBuilder.DropIndex(
                name: "IX_student_profiles_ParentTrackingCode",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "HasSeenTrackingCodePopup",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "ParentTrackingCode",
                table: "student_profiles");
        }
    }
}
