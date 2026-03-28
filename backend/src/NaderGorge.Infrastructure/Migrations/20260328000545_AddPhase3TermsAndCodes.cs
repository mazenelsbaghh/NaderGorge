using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3TermsAndCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_content_sections_packages_PackageId",
                table: "content_sections");

            migrationBuilder.DropForeignKey(
                name: "FK_student_access_grants_access_codes_AccessCodeId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "City",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "School",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "Track",
                table: "student_profiles");

            migrationBuilder.RenameColumn(
                name: "PackageId",
                table: "content_sections",
                newName: "TermId");

            migrationBuilder.RenameIndex(
                name: "IX_content_sections_PackageId",
                table: "content_sections",
                newName: "IX_content_sections_TermId");

            migrationBuilder.AlterColumn<string>(
                name: "ParentPhone",
                table: "student_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Governorate",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "student_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "student_profiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "EducationStage",
                table: "student_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "student_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GradeLevel",
                table: "student_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFatherAlive",
                table: "student_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMotherAlive",
                table: "student_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StudentCode",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StudyTrack",
                table: "student_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccessCodeId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ContentSectionId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExamId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrantType",
                table: "student_access_grants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LessonVideoId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TermId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoTag",
                table: "lesson_videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceAmount",
                table: "code_groups",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeType",
                table: "code_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ContentSectionId",
                table: "code_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "code_groups",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExamId",
                table: "code_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "code_groups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrDataGenerated",
                table: "code_groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TermId",
                table: "code_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "access_codes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCodeUrl",
                table: "access_codes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "code_video_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_video_targets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_video_targets_code_groups_CodeGroupId",
                        column: x => x.CodeGroupId,
                        principalTable: "code_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_code_video_targets_lesson_videos_LessonVideoId",
                        column: x => x.LessonVideoId,
                        principalTable: "lesson_videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_balances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_balances_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_terms_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "balance_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_balance_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_balance_transactions_student_balances_StudentBalanceId",
                        column: x => x.StudentBalanceId,
                        principalTable: "student_balances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_balance_transactions_StudentBalanceId",
                table: "balance_transactions",
                column: "StudentBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_code_video_targets_CodeGroupId_LessonVideoId",
                table: "code_video_targets",
                columns: new[] { "CodeGroupId", "LessonVideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_code_video_targets_LessonVideoId",
                table: "code_video_targets",
                column: "LessonVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_student_balances_UserId",
                table: "student_balances",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terms_PackageId",
                table: "terms",
                column: "PackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_content_sections_terms_TermId",
                table: "content_sections",
                column: "TermId",
                principalTable: "terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_student_access_grants_access_codes_AccessCodeId",
                table: "student_access_grants",
                column: "AccessCodeId",
                principalTable: "access_codes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_content_sections_terms_TermId",
                table: "content_sections");

            migrationBuilder.DropForeignKey(
                name: "FK_student_access_grants_access_codes_AccessCodeId",
                table: "student_access_grants");

            migrationBuilder.DropTable(
                name: "balance_transactions");

            migrationBuilder.DropTable(
                name: "code_video_targets");

            migrationBuilder.DropTable(
                name: "terms");

            migrationBuilder.DropTable(
                name: "student_balances");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "EducationStage",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "GradeLevel",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "IsFatherAlive",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "IsMotherAlive",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "StudentCode",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "StudyTrack",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "ContentSectionId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "ExamId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "GrantType",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "LessonVideoId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "TermId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "VideoTag",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "BalanceAmount",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "CodeType",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "ContentSectionId",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "ExamId",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "QrDataGenerated",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "TermId",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "access_codes");

            migrationBuilder.DropColumn(
                name: "QrCodeUrl",
                table: "access_codes");

            migrationBuilder.RenameColumn(
                name: "TermId",
                table: "content_sections",
                newName: "PackageId");

            migrationBuilder.RenameIndex(
                name: "IX_content_sections_TermId",
                table: "content_sections",
                newName: "IX_content_sections_PackageId");

            migrationBuilder.AlterColumn<string>(
                name: "ParentPhone",
                table: "student_profiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Governorate",
                table: "student_profiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "student_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "student_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "School",
                table: "student_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Track",
                table: "student_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccessCodeId",
                table: "student_access_grants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_content_sections_packages_PackageId",
                table: "content_sections",
                column: "PackageId",
                principalTable: "packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_student_access_grants_access_codes_AccessCodeId",
                table: "student_access_grants",
                column: "AccessCodeId",
                principalTable: "access_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
