using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningWatchProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LearningWatchedSeconds",
                table: "video_watch_events",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // Preserve recorded media time only. Historical quota views do not
            // prove a full viewing, and unrecorded time cannot be reconstructed.
            migrationBuilder.Sql("""
                UPDATE video_watch_events
                SET "LearningWatchedSeconds" = GREATEST(0, "TimeWatchedInSeconds");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LearningWatchedSeconds",
                table: "video_watch_events");
        }
    }
}
