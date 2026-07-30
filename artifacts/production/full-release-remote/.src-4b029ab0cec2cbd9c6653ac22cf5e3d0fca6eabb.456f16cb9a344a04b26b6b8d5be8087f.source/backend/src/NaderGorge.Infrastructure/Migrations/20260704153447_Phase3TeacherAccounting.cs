using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3TeacherAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntroVideoUrl",
                table: "teacher_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublicProfileEnabled",
                table: "teacher_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicBio",
                table: "teacher_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "teacher_profiles",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RatingAverage",
                table: "teacher_profiles",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "teacher_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "teacher_payouts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "teacher_payouts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "teacher_payouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "teacher_payouts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByUserId",
                table: "teacher_payouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferReference",
                table: "teacher_payouts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "community_posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shared_teacher_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AvailableUntil = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DistributionMode = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_teacher_packages", x => x.Id);
                    table.CheckConstraint("CK_shared_teacher_packages_price", "\"Price\" > 0");
                    table.ForeignKey(
                        name: "FK_shared_teacher_packages_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shared_teacher_packages_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "teacher_financial_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PromotionalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlatformShareAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EGP"),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false),
                    PayoutStatus = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_financial_events", x => x.Id);
                    table.CheckConstraint("CK_teacher_financial_events_amounts", "\"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"PromotionalAmount\" >= 0 AND \"PlatformShareAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_teacher_financial_events_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shared_teacher_package_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedTeacherPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<int>(type: "integer", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_teacher_package_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_teacher_package_items_shared_teacher_packages_Shared~",
                        column: x => x.SharedTeacherPackageId,
                        principalTable: "shared_teacher_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_teacher_package_items_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_shared_teacher_package_items_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shared_teacher_package_teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedTeacherPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllocationMode = table.Column<int>(type: "integer", nullable: false),
                    AllocationValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_teacher_package_teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_teacher_package_teachers_shared_teacher_packages_Sha~",
                        column: x => x.SharedTeacherPackageId,
                        principalTable: "shared_teacher_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_teacher_package_teachers_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_shared_teacher_package_teachers_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teacher_financial_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherFinancialEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationMode = table.Column<int>(type: "integer", nullable: false),
                    AllocationValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GrossBasisAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TeacherShareAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlatformShareAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StudentNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StudentPhoneSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContentNameSnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CodeSerialNumber = table.Column<long>(type: "bigint", nullable: true),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false),
                    PayoutStatus = table.Column<int>(type: "integer", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_financial_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_financial_allocations_teacher_financial_events_Teac~",
                        column: x => x.TeacherFinancialEventId,
                        principalTable: "teacher_financial_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teacher_financial_allocations_teacher_payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "teacher_payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_teacher_financial_allocations_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teacher_payout_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedFinancialEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedPayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_payout_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_payout_adjustments_teacher_financial_events_Related~",
                        column: x => x.RelatedFinancialEventId,
                        principalTable: "teacher_financial_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_teacher_payout_adjustments_teacher_payouts_RelatedPayoutId",
                        column: x => x.RelatedPayoutId,
                        principalTable: "teacher_payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_teacher_payout_adjustments_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_PublicSlug",
                table: "teacher_profiles",
                column: "PublicSlug",
                unique: true,
                filter: "\"PublicSlug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payouts_ApprovedByUserId",
                table: "teacher_payouts",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payouts_PaidByUserId",
                table: "teacher_payouts",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_TeacherId",
                table: "community_posts",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_package_items_SharedTeacherPackageId_Content~",
                table: "shared_teacher_package_items",
                columns: new[] { "SharedTeacherPackageId", "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_package_items_SubjectId",
                table: "shared_teacher_package_items",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_package_items_TeacherId",
                table: "shared_teacher_package_items",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_package_teachers_SharedTeacherPackageId_Teac~",
                table: "shared_teacher_package_teachers",
                columns: new[] { "SharedTeacherPackageId", "TeacherId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_package_teachers_SubjectId",
                table: "shared_teacher_package_teachers",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_package_teachers_TeacherId",
                table: "shared_teacher_package_teachers",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_packages_CreatedByUserId",
                table: "shared_teacher_packages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_packages_IsPublished_AvailableFrom_Available~",
                table: "shared_teacher_packages",
                columns: new[] { "IsPublished", "AvailableFrom", "AvailableUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_packages_Slug",
                table: "shared_teacher_packages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_teacher_packages_UpdatedByUserId",
                table: "shared_teacher_packages",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_allocations_PayoutId",
                table: "teacher_financial_allocations",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_allocations_TeacherFinancialEventId",
                table: "teacher_financial_allocations",
                column: "TeacherFinancialEventId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_allocations_TeacherId_CreatedAt",
                table: "teacher_financial_allocations",
                columns: new[] { "TeacherId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_allocations_TeacherId_ReviewStatus_Payout~",
                table: "teacher_financial_allocations",
                columns: new[] { "TeacherId", "ReviewStatus", "PayoutStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_events_IdempotencyKey",
                table: "teacher_financial_events",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_events_ReviewStatus_PayoutStatus_Occurred~",
                table: "teacher_financial_events",
                columns: new[] { "ReviewStatus", "PayoutStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_events_StudentId",
                table: "teacher_financial_events",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_events_TargetType_TargetId",
                table: "teacher_financial_events",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payout_adjustments_RelatedFinancialEventId",
                table: "teacher_payout_adjustments",
                column: "RelatedFinancialEventId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payout_adjustments_RelatedPayoutId",
                table: "teacher_payout_adjustments",
                column: "RelatedPayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payout_adjustments_TeacherId_Status",
                table: "teacher_payout_adjustments",
                columns: new[] { "TeacherId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_community_posts_teacher_profiles_TeacherId",
                table: "community_posts",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_payouts_users_ApprovedByUserId",
                table: "teacher_payouts",
                column: "ApprovedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_payouts_users_PaidByUserId",
                table: "teacher_payouts",
                column: "PaidByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_community_posts_teacher_profiles_TeacherId",
                table: "community_posts");

            migrationBuilder.DropForeignKey(
                name: "FK_teacher_payouts_users_ApprovedByUserId",
                table: "teacher_payouts");

            migrationBuilder.DropForeignKey(
                name: "FK_teacher_payouts_users_PaidByUserId",
                table: "teacher_payouts");

            migrationBuilder.DropTable(
                name: "shared_teacher_package_items");

            migrationBuilder.DropTable(
                name: "shared_teacher_package_teachers");

            migrationBuilder.DropTable(
                name: "teacher_financial_allocations");

            migrationBuilder.DropTable(
                name: "teacher_payout_adjustments");

            migrationBuilder.DropTable(
                name: "shared_teacher_packages");

            migrationBuilder.DropTable(
                name: "teacher_financial_events");

            migrationBuilder.DropIndex(
                name: "IX_teacher_profiles_PublicSlug",
                table: "teacher_profiles");

            migrationBuilder.DropIndex(
                name: "IX_teacher_payouts_ApprovedByUserId",
                table: "teacher_payouts");

            migrationBuilder.DropIndex(
                name: "IX_teacher_payouts_PaidByUserId",
                table: "teacher_payouts");

            migrationBuilder.DropIndex(
                name: "IX_community_posts_TeacherId",
                table: "community_posts");

            migrationBuilder.DropColumn(
                name: "IntroVideoUrl",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "IsPublicProfileEnabled",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "PublicBio",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "RatingAverage",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "teacher_payouts");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "teacher_payouts");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "teacher_payouts");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "teacher_payouts");

            migrationBuilder.DropColumn(
                name: "PaidByUserId",
                table: "teacher_payouts");

            migrationBuilder.DropColumn(
                name: "TransferReference",
                table: "teacher_payouts");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "community_posts");
        }
    }
}
