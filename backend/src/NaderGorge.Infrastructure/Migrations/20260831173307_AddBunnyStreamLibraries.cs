using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBunnyStreamLibraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bunny_video_assets_BunnyVideoGuid",
                table: "bunny_video_assets");

            migrationBuilder.AddColumn<Guid>(
                name: "BunnyStreamLibraryId",
                table: "lesson_videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ActivateWhenReady",
                table: "bunny_video_assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "bunny_stream_libraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalLibraryId = table.Column<long>(type: "bigint", nullable: false),
                    ApiKeyCiphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bunny_stream_libraries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "bunny_stream_libraries",
                columns: new[] { "Id", "ApiKeyCiphertext", "CreatedAt", "ExternalLibraryId", "IsActive", "LastValidatedAtUtc", "Name", "NormalizedName", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a5d123ac-0b9f-4f69-9d15-740733000001"), null, new DateTime(2026, 8, 31, 0, 0, 0, 0, DateTimeKind.Utc), 740733L, true, null, "أولى", "أولى", null },
                    { new Guid("a5d123ac-0b9f-4f69-9d15-740737000002"), null, new DateTime(2026, 8, 31, 0, 0, 0, 0, DateTimeKind.Utc), 740737L, true, null, "ثانية", "ثانية", null },
                    { new Guid("a5d123ac-0b9f-4f69-9d15-740801000003"), null, new DateTime(2026, 8, 31, 0, 0, 0, 0, DateTimeKind.Utc), 740801L, true, null, "مسار", "مسار", null }
                });

            // Existing Bunny references were previously resolved through one process-wide
            // library. Preserve that ownership explicitly before enforcing the new relation.
            migrationBuilder.Sql(
                """
                UPDATE "lesson_videos"
                SET "BunnyStreamLibraryId" = 'a5d123ac-0b9f-4f69-9d15-740733000001'
                WHERE LOWER("Provider") = 'bunny';
                """);

            // Preserve whether each existing managed upload should become active after a
            // pending transcode completes while migrating those assets to the first library.
            migrationBuilder.Sql(
                """
                UPDATE "bunny_video_assets" AS asset
                SET "BunnyLibraryId" = 740733,
                    "ActivateWhenReady" = video."IsActive"
                FROM "lesson_videos" AS video
                WHERE video."Id" = asset."LessonVideoId";
                """);

            // Old application replicas do not send the new column during a rolling
            // deployment. This database-only compatibility path keeps those writes
            // valid without exposing a default library in the new application flows.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION "set_bunny_library_for_legacy_writes"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF LOWER(NEW."Provider") = 'bunny'
                       AND NEW."BunnyStreamLibraryId" IS NULL THEN
                        NEW."BunnyStreamLibraryId" := 'a5d123ac-0b9f-4f69-9d15-740733000001';
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER "TR_lesson_videos_bunny_library_legacy"
                BEFORE INSERT OR UPDATE OF "Provider", "BunnyStreamLibraryId"
                ON "lesson_videos"
                FOR EACH ROW
                EXECUTE FUNCTION "set_bunny_library_for_legacy_writes"();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_videos_BunnyStreamLibraryId",
                table: "lesson_videos",
                column: "BunnyStreamLibraryId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lesson_videos_bunny_library",
                table: "lesson_videos",
                sql: "LOWER(\"Provider\") <> 'bunny' OR \"BunnyStreamLibraryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_BunnyLibraryId_BunnyVideoGuid",
                table: "bunny_video_assets",
                columns: new[] { "BunnyLibraryId", "BunnyVideoGuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bunny_stream_libraries_ExternalLibraryId",
                table: "bunny_stream_libraries",
                column: "ExternalLibraryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bunny_stream_libraries_NormalizedName",
                table: "bunny_stream_libraries",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_videos_bunny_stream_libraries_BunnyStreamLibraryId",
                table: "lesson_videos",
                column: "BunnyStreamLibraryId",
                principalTable: "bunny_stream_libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lesson_videos_bunny_stream_libraries_BunnyStreamLibraryId",
                table: "lesson_videos");

            migrationBuilder.Sql(
                """
                DROP TRIGGER "TR_lesson_videos_bunny_library_legacy" ON "lesson_videos";
                DROP FUNCTION "set_bunny_library_for_legacy_writes"();
                """);

            migrationBuilder.DropTable(
                name: "bunny_stream_libraries");

            migrationBuilder.DropIndex(
                name: "IX_lesson_videos_BunnyStreamLibraryId",
                table: "lesson_videos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lesson_videos_bunny_library",
                table: "lesson_videos");

            migrationBuilder.DropIndex(
                name: "IX_bunny_video_assets_BunnyLibraryId_BunnyVideoGuid",
                table: "bunny_video_assets");

            migrationBuilder.DropColumn(
                name: "BunnyStreamLibraryId",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "ActivateWhenReady",
                table: "bunny_video_assets");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_BunnyVideoGuid",
                table: "bunny_video_assets",
                column: "BunnyVideoGuid",
                unique: true);
        }
    }
}
