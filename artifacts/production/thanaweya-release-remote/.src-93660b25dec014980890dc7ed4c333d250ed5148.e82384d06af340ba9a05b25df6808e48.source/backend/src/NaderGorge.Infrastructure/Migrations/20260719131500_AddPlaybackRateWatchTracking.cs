using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

public partial class AddPlaybackRateWatchTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "ActualWatchedSeconds", table: "video_watch_events", type: "numeric", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(name: "LastPlaybackRate", table: "video_watch_events", type: "numeric", nullable: false, defaultValue: 1m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ActualWatchedSeconds", table: "video_watch_events");
        migrationBuilder.DropColumn(name: "LastPlaybackRate", table: "video_watch_events");
    }
}
