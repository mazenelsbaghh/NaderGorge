using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBunnyVideoProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bunny_video_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BunnyLibraryId = table.Column<long>(type: "bigint", nullable: false),
                    BunnyVideoGuid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BunnyCollectionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UploadMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceUrlHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    StorageBytes = table.Column<long>(type: "bigint", nullable: true),
                    BandwidthBytes = table.Column<long>(type: "bigint", nullable: true),
                    BunnyEncodeProgress = table.Column<int>(type: "integer", nullable: true),
                    LastStatusSyncedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastUsageSyncedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bunny_video_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bunny_video_assets_lesson_videos_LessonVideoId",
                        column: x => x.LessonVideoId,
                        principalTable: "lesson_videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bunny_video_assets_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bunny_video_assets_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bunny_video_assets_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bunny_video_assets_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bunny_usage_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BunnyVideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StorageBytes = table.Column<long>(type: "bigint", nullable: false),
                    BandwidthBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsBandwidthEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    BandwidthSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StorageRateUsdPerGb = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BandwidthRateUsdPerGb = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    StorageCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BandwidthCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BunnyStorageCalculatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SyncedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bunny_usage_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bunny_usage_snapshots_bunny_video_assets_BunnyVideoAssetId",
                        column: x => x.BunnyVideoAssetId,
                        principalTable: "bunny_video_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bunny_usage_snapshots_users_SyncedByUserId",
                        column: x => x.SyncedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bunny_usage_snapshots_BunnyVideoAssetId_PeriodStartUtc_Peri~",
                table: "bunny_usage_snapshots",
                columns: new[] { "BunnyVideoAssetId", "PeriodStartUtc", "PeriodEndUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bunny_usage_snapshots_PackageId_PeriodStartUtc_PeriodEndUtc",
                table: "bunny_usage_snapshots",
                columns: new[] { "PackageId", "PeriodStartUtc", "PeriodEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bunny_usage_snapshots_SyncedByUserId",
                table: "bunny_usage_snapshots",
                column: "SyncedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_usage_snapshots_TeacherId_PeriodStartUtc_PeriodEndUtc",
                table: "bunny_usage_snapshots",
                columns: new[] { "TeacherId", "PeriodStartUtc", "PeriodEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_BunnyVideoGuid",
                table: "bunny_video_assets",
                column: "BunnyVideoGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_LessonId",
                table: "bunny_video_assets",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_LessonVideoId",
                table: "bunny_video_assets",
                column: "LessonVideoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_PackageId",
                table: "bunny_video_assets",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_Status_LastStatusSyncedAtUtc",
                table: "bunny_video_assets",
                columns: new[] { "Status", "LastStatusSyncedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_TeacherId_PackageId_LessonId",
                table: "bunny_video_assets",
                columns: new[] { "TeacherId", "PackageId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_bunny_video_assets_UploadedByUserId",
                table: "bunny_video_assets",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bunny_usage_snapshots");

            migrationBuilder.DropTable(
                name: "bunny_video_assets");
        }
    }
}
