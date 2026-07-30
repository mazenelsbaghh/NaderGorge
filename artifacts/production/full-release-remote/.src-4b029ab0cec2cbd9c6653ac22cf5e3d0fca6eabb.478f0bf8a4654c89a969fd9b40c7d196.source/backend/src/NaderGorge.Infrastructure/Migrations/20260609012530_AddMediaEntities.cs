using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MediaPipelineId",
                table: "task_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_production_pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    AssignedAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetFolderUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EditingErrorCount = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_production_pipelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_media_production_pipelines_users_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "social_media_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Script = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    MediaProductionPipelineId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_social_media_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_social_media_plans_media_production_pipelines_MediaProducti~",
                        column: x => x.MediaProductionPipelineId,
                        principalTable: "media_production_pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_MediaPipelineId",
                table: "task_items",
                column: "MediaPipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_media_production_pipelines_AssignedAgentId",
                table: "media_production_pipelines",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_media_production_pipelines_Stage",
                table: "media_production_pipelines",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_social_media_plans_MediaProductionPipelineId",
                table: "social_media_plans",
                column: "MediaProductionPipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_social_media_plans_ScheduledDate",
                table: "social_media_plans",
                column: "ScheduledDate");

            migrationBuilder.AddForeignKey(
                name: "FK_task_items_media_production_pipelines_MediaPipelineId",
                table: "task_items",
                column: "MediaPipelineId",
                principalTable: "media_production_pipelines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_items_media_production_pipelines_MediaPipelineId",
                table: "task_items");

            migrationBuilder.DropTable(
                name: "social_media_plans");

            migrationBuilder.DropTable(
                name: "media_production_pipelines");

            migrationBuilder.DropIndex(
                name: "IX_task_items_MediaPipelineId",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "MediaPipelineId",
                table: "task_items");
        }
    }
}
