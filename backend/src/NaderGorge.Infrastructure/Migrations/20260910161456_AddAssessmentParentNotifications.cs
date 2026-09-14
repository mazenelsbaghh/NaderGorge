using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentParentNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ParentNotificationEnabledAt",
                table: "homeworks",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentNotificationSettingsJson",
                table: "homeworks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParentNotificationEnabledAt",
                table: "exams",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentNotificationSettingsJson",
                table: "exams",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assessment_parent_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DestinationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MetaMessageId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_parent_deliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_parent_deliveries_AssessmentKind_AttemptId",
                table: "assessment_parent_deliveries",
                columns: new[] { "AssessmentKind", "AttemptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assessment_parent_deliveries_Status_CreatedAt",
                table: "assessment_parent_deliveries",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_parent_deliveries");

            migrationBuilder.DropColumn(
                name: "ParentNotificationEnabledAt",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "ParentNotificationSettingsJson",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "ParentNotificationEnabledAt",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "ParentNotificationSettingsJson",
                table: "exams");
        }
    }
}
