using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotVideoPlaybackTrackingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedWallSeconds",
                table: "VideoPlaybackSessions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpeedAdjustedSecondsRemainder",
                table: "VideoPlaybackSessions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TrackingDurationSeconds",
                table: "VideoPlaybackSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackingThresholdPercentage",
                table: "VideoPlaybackSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackingThresholdSeconds",
                table: "VideoPlaybackSessions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedWallSeconds",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "SpeedAdjustedSecondsRemainder",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "TrackingDurationSeconds",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "TrackingThresholdPercentage",
                table: "VideoPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "TrackingThresholdSeconds",
                table: "VideoPlaybackSessions");
        }
    }
}
