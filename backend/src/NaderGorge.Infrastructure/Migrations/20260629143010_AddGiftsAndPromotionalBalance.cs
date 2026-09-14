using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftsAndPromotionalBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GiftRecipientId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUses",
                table: "student_access_grants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsesConsumed",
                table: "student_access_grants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "gift_issuances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonVideoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_issuances", x => x.Id);
                    table.CheckConstraint("CK_gift_issuances_max_uses", "\"MaxUses\" IS NULL OR \"MaxUses\" > 0");
                    table.CheckConstraint("CK_gift_issuances_target", "(\"TargetType\" = 0 AND \"PackageId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 1 AND \"PackageId\" IS NULL AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 2 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NOT NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 3 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NOT NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR (\"TargetType\" = 4 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" > 0) OR (\"TargetType\" = 5 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NOT NULL AND \"Amount\" > 0)");
                    table.ForeignKey(
                        name: "FK_gift_issuances_exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_issuances_lesson_videos_LessonVideoId",
                        column: x => x.LessonVideoId,
                        principalTable: "lesson_videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_issuances_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_issuances_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_issuances_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_issuances_users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gift_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GiftIssuanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OutcomeCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OutcomeMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UsesConsumed = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_recipients", x => x.Id);
                    table.CheckConstraint("CK_gift_recipients_uses", "\"UsesConsumed\" >= 0");
                    table.ForeignKey(
                        name: "FK_gift_recipients_gift_issuances_GiftIssuanceId",
                        column: x => x.GiftIssuanceId,
                        principalTable: "gift_issuances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_recipients_users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_gift_recipients_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotional_balance_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GiftRecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AvailableAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ConsumedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpiredAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RevokedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MaxPurchaseCount = table.Column<int>(type: "integer", nullable: true),
                    PurchaseCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotional_balance_allocations", x => x.Id);
                    table.CheckConstraint("CK_promotional_balance_conservation", "\"OriginalAmount\" > 0 AND \"AvailableAmount\" >= 0 AND \"ConsumedAmount\" >= 0 AND \"ExpiredAmount\" >= 0 AND \"RevokedAmount\" >= 0 AND \"OriginalAmount\" = \"AvailableAmount\" + \"ConsumedAmount\" + \"ExpiredAmount\" + \"RevokedAmount\"");
                    table.CheckConstraint("CK_promotional_balance_purchase_count", "\"PurchaseCount\" >= 0 AND (\"MaxPurchaseCount\" IS NULL OR (\"MaxPurchaseCount\" > 0 AND \"PurchaseCount\" <= \"MaxPurchaseCount\"))");
                    table.ForeignKey(
                        name: "FK_promotional_balance_allocations_gift_recipients_GiftRecipie~",
                        column: x => x.GiftRecipientId,
                        principalTable: "gift_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotional_balance_allocations_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotional_balance_allocations_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotional_balance_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GiftRecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<int>(type: "integer", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotional_balance_usages", x => x.Id);
                    table.CheckConstraint("CK_promotional_balance_usage_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_promotional_balance_usages_gift_recipients_GiftRecipientId",
                        column: x => x.GiftRecipientId,
                        principalTable: "gift_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotional_balance_usages_promotional_balance_allocations_~",
                        column: x => x.AllocationId,
                        principalTable: "promotional_balance_allocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_GiftRecipientId",
                table: "student_access_grants",
                column: "GiftRecipientId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_student_access_grants_gift_uses",
                table: "student_access_grants",
                sql: "\"UsesConsumed\" >= 0 AND (\"MaxUses\" IS NULL OR (\"MaxUses\" > 0 AND \"UsesConsumed\" <= \"MaxUses\"))");

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_CreatedAt_Status",
                table: "gift_issuances",
                columns: new[] { "CreatedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_ExamId",
                table: "gift_issuances",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_IssuedByUserId",
                table: "gift_issuances",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_LessonId",
                table: "gift_issuances",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_LessonVideoId",
                table: "gift_issuances",
                column: "LessonVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_PackageId",
                table: "gift_issuances",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_RequestId",
                table: "gift_issuances",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_issuances_TeacherId",
                table: "gift_issuances",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_recipients_GiftIssuanceId_StudentId",
                table: "gift_recipients",
                columns: new[] { "GiftIssuanceId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_recipients_RevokedByUserId",
                table: "gift_recipients",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_recipients_StudentId",
                table: "gift_recipients",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_promotional_balance_allocations_GiftRecipientId",
                table: "promotional_balance_allocations",
                column: "GiftRecipientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotional_balance_allocations_StudentId_TeacherId_Status_~",
                table: "promotional_balance_allocations",
                columns: new[] { "StudentId", "TeacherId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_promotional_balance_allocations_TeacherId",
                table: "promotional_balance_allocations",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_promotional_balance_usages_AllocationId",
                table: "promotional_balance_usages",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_promotional_balance_usages_GiftRecipientId",
                table: "promotional_balance_usages",
                column: "GiftRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_promotional_balance_usages_PurchaseOperationId_AllocationId",
                table: "promotional_balance_usages",
                columns: new[] { "PurchaseOperationId", "AllocationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_student_access_grants_gift_recipients_GiftRecipientId",
                table: "student_access_grants",
                column: "GiftRecipientId",
                principalTable: "gift_recipients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_student_access_grants_gift_recipients_GiftRecipientId",
                table: "student_access_grants");

            migrationBuilder.DropTable(
                name: "promotional_balance_usages");

            migrationBuilder.DropTable(
                name: "promotional_balance_allocations");

            migrationBuilder.DropTable(
                name: "gift_recipients");

            migrationBuilder.DropTable(
                name: "gift_issuances");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_GiftRecipientId",
                table: "student_access_grants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_student_access_grants_gift_uses",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "GiftRecipientId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "MaxUses",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "UsesConsumed",
                table: "student_access_grants");
        }
    }
}
