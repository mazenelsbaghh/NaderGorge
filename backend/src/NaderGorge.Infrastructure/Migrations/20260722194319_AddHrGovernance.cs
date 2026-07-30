using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_migration_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    SourceCount = table.Column<int>(type: "integer", nullable: false),
                    TargetCount = table.Column<int>(type: "integer", nullable: false),
                    SourceTotal = table.Column<decimal>(type: "numeric(24,4)", nullable: false),
                    TargetTotal = table.Column<decimal>(type: "numeric(24,4)", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReportJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_migration_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_migration_conflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MigrationBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_migration_conflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_migration_conflicts_hr_migration_batches_MigrationBatchId",
                        column: x => x.MigrationBatchId,
                        principalTable: "hr_migration_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_migration_record_maps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MigrationBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_migration_record_maps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_migration_record_maps_hr_migration_batches_MigrationBatc~",
                        column: x => x.MigrationBatchId,
                        principalTable: "hr_migration_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_migration_batches_Module_RequestHash",
                table: "hr_migration_batches",
                columns: new[] { "Module", "RequestHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_migration_batches_Module_State_CreatedAt",
                table: "hr_migration_batches",
                columns: new[] { "Module", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_migration_conflicts_MigrationBatchId_SourceType_SourceId~",
                table: "hr_migration_conflicts",
                columns: new[] { "MigrationBatchId", "SourceType", "SourceId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_migration_conflicts_MigrationBatchId_State",
                table: "hr_migration_conflicts",
                columns: new[] { "MigrationBatchId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_migration_record_maps_MigrationBatchId_TargetType_Target~",
                table: "hr_migration_record_maps",
                columns: new[] { "MigrationBatchId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_migration_record_maps_SourceType_SourceId",
                table: "hr_migration_record_maps",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_migration_conflicts");

            migrationBuilder.DropTable(
                name: "hr_migration_record_maps");

            migrationBuilder.DropTable(
                name: "hr_migration_batches");
        }
    }
}
