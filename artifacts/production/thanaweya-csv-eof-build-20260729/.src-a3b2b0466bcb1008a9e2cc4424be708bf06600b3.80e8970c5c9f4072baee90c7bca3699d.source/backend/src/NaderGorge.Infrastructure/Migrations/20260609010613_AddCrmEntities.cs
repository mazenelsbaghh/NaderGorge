using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crm_call_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NextFollowUpDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_call_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_call_logs_users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crm_call_logs_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crm_student_statuses",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    NextFollowUpDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastCalledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_student_statuses", x => x.StudentId);
                    table.ForeignKey(
                        name: "FK_crm_student_statuses_users_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crm_student_statuses_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crm_call_logs_AgentId",
                table: "crm_call_logs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_call_logs_CallDate",
                table: "crm_call_logs",
                column: "CallDate");

            migrationBuilder.CreateIndex(
                name: "IX_crm_call_logs_StudentId",
                table: "crm_call_logs",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_student_statuses_AssignedAgentId",
                table: "crm_student_statuses",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_student_statuses_NextFollowUpDate",
                table: "crm_student_statuses",
                column: "NextFollowUpDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crm_call_logs");

            migrationBuilder.DropTable(
                name: "crm_student_statuses");
        }
    }
}
