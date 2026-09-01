using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProtectBunnyReplacementSourceRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceRevision",
                table: "lesson_videos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetSourceRevision",
                table: "bunny_video_assets",
                type: "integer",
                nullable: true);

            // Pending replacements created before this migration must capture the
            // source revision they were created against.  Future source changes
            // then reliably prevent an old candidate from being promoted.
            migrationBuilder.Sql("""
                UPDATE bunny_video_assets AS asset
                SET "TargetSourceRevision" = video."SourceRevision"
                FROM lesson_videos AS video
                WHERE asset."LessonVideoId" = video."Id"
                  AND asset."SourceState" = 1
                  AND asset."TargetSourceRevision" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceRevision",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "TargetSourceRevision",
                table: "bunny_video_assets");
        }
    }
}
