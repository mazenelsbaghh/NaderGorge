using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoRepairIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoRepairControls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Paused = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDeploy = table.Column<bool>(type: "boolean", nullable: false),
                    Heartbeat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Runner = table.Column<string>(type: "text", nullable: false),
                    LogCursor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoRepairControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoRepairIncidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Occurrences = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposalHash = table.Column<string>(type: "text", nullable: false),
                    ApprovedHash = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    ReleaseId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoRepairIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoRepairLogReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoRepairLogReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoRepairEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoRepairEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoRepairEvents_AutoRepairIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "AutoRepairIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AutoRepairControls",
                columns: new[] { "Id", "AutoDeploy", "Heartbeat", "LogCursor", "Paused", "Runner" },
                values: new object[] { 1, false, null, null, true, "" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoRepairEvents_IncidentId_Id",
                table: "AutoRepairEvents",
                columns: new[] { "IncidentId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoRepairIncidents_Fingerprint",
                table: "AutoRepairIncidents",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutoRepairIncidents_Status_FirstSeen",
                table: "AutoRepairIncidents",
                columns: new[] { "Status", "FirstSeen" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoRepairLogReceipts_Timestamp",
                table: "AutoRepairLogReceipts",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoRepairControls");

            migrationBuilder.DropTable(
                name: "AutoRepairEvents");

            migrationBuilder.DropTable(
                name: "AutoRepairLogReceipts");

            migrationBuilder.DropTable(
                name: "AutoRepairIncidents");
        }
    }
}
