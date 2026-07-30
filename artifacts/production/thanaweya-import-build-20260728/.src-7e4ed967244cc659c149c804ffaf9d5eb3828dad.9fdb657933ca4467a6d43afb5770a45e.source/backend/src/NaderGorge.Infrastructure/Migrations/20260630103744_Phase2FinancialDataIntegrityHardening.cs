using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2FinancialDataIntegrityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_balance_transactions_student_balances_StudentBalanceId",
                table: "balance_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_student_balances_users_UserId",
                table: "student_balances");

            migrationBuilder.DropForeignKey(
                name: "FK_teacher_accounts_teacher_profiles_TeacherId",
                table: "teacher_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_teacher_payouts_teacher_profiles_TeacherId",
                table: "teacher_payouts");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_PackageId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_recharge_requests_WalletId",
                table: "recharge_requests");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "users",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SecurityStampVersion",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedBalance",
                table: "teacher_accounts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "teacher_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "student_balances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_teacher_accounts_balances_non_negative",
                table: "teacher_accounts",
                sql: "\"TotalEarnings\" >= 0 AND \"CurrentBalance\" >= 0 AND \"ReservedBalance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_teacher_accounts_reserved_available",
                table: "teacher_accounts",
                sql: "\"ReservedBalance\" <= \"CurrentBalance\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_student_balances_non_negative",
                table: "student_balances",
                sql: "\"CurrentBalance\" >= 0");

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                "UserId",
                                "GrantType",
                                COALESCE("PackageId", "TermId", "ContentSectionId", "LessonId", "LessonVideoId", "ExamId")
                            ORDER BY
                                CASE WHEN "ExpiresAt" IS NULL THEN 1 ELSE 0 END DESC,
                                "ExpiresAt" DESC NULLS FIRST,
                                "GrantedAt" DESC,
                                "CreatedAt" DESC,
                                "Id" DESC
                        ) AS rn
                    FROM student_access_grants
                    WHERE "IsActive" = TRUE
                      AND COALESCE("PackageId", "TermId", "ContentSectionId", "LessonId", "LessonVideoId", "ExamId") IS NOT NULL
                )
                UPDATE student_access_grants sag
                SET
                    "IsActive" = FALSE,
                    "CancelledAt" = COALESCE(sag."CancelledAt", NOW() AT TIME ZONE 'UTC'),
                    "CancellationReason" = COALESCE(sag."CancellationReason", 'Merged duplicate active grant during financial integrity migration.'),
                    "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
                FROM ranked
                WHERE sag."Id" = ranked."Id"
                  AND ranked.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_Content~",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "AccessCodeId", "ContentSectionId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"ContentSectionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_ExamId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "AccessCodeId", "ExamId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"ExamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_LessonId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "AccessCodeId", "LessonId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"LessonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_LessonV~",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "AccessCodeId", "LessonVideoId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"LessonVideoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_Package~",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "AccessCodeId", "PackageId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"PackageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_TermId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "AccessCodeId", "TermId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"TermId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_ContentSectionId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "ContentSectionId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"GrantType\" = 2 AND \"ContentSectionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_ExamId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "ExamId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"GrantType\" = 5 AND \"ExamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_LessonId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "LessonId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"GrantType\" = 3 AND \"LessonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_LessonVideoId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "LessonVideoId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"GrantType\" = 4 AND \"LessonVideoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_GrantType_TermId",
                table: "student_access_grants",
                columns: new[] { "UserId", "GrantType", "TermId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"GrantType\" = 1 AND \"TermId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_PackageId",
                table: "student_access_grants",
                columns: new[] { "UserId", "PackageId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"PackageId\" IS NOT NULL AND \"GrantType\" = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_student_access_grants_target_shape",
                table: "student_access_grants",
                sql: "(\"GrantType\" = 0 AND \"PackageId\" IS NOT NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR (\"GrantType\" = 1 AND \"TermId\" IS NOT NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR (\"GrantType\" = 2 AND \"ContentSectionId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR (\"GrantType\" = 3 AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR (\"GrantType\" = 4 AND \"LessonVideoId\" IS NOT NULL AND \"ExamId\" IS NULL) OR (\"GrantType\" = 5 AND \"ExamId\" IS NOT NULL AND \"LessonVideoId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_WalletId_Status_Amount_SenderPhoneNumber_~",
                table: "recharge_requests",
                columns: new[] { "WalletId", "Status", "Amount", "SenderPhoneNumber", "CreatedAt" },
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_incoming_sms_logs_MatchedRechargeRequestId",
                table: "incoming_sms_logs",
                column: "MatchedRechargeRequestId",
                unique: true,
                filter: "\"MatchedRechargeRequestId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_incoming_sms_logs_match_consistency",
                table: "incoming_sms_logs",
                sql: "(\"IsMatched\" = FALSE AND \"MatchedRechargeRequestId\" IS NULL) OR (\"IsMatched\" = TRUE AND \"MatchedRechargeRequestId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_digital_wallets_current_balance_non_negative",
                table: "digital_wallets",
                sql: "\"CurrentBalance\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_balance_transactions_TransactionType_ReferenceId",
                table: "balance_transactions",
                columns: new[] { "TransactionType", "ReferenceId" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL AND \"TransactionType\" IN ('DigitalRecharge', 'CodeRedemption')");

            migrationBuilder.AddForeignKey(
                name: "FK_balance_transactions_student_balances_StudentBalanceId",
                table: "balance_transactions",
                column: "StudentBalanceId",
                principalTable: "student_balances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_student_balances_users_UserId",
                table: "student_balances",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_accounts_teacher_profiles_TeacherId",
                table: "teacher_accounts",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_payouts_teacher_profiles_TeacherId",
                table: "teacher_payouts",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_balance_transactions_student_balances_StudentBalanceId",
                table: "balance_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_student_balances_users_UserId",
                table: "student_balances");

            migrationBuilder.DropForeignKey(
                name: "FK_teacher_accounts_teacher_profiles_TeacherId",
                table: "teacher_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_teacher_payouts_teacher_profiles_TeacherId",
                table: "teacher_payouts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_teacher_accounts_balances_non_negative",
                table: "teacher_accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_teacher_accounts_reserved_available",
                table: "teacher_accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_student_balances_non_negative",
                table: "student_balances");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_Content~",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_ExamId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_LessonId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_LessonV~",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_Package~",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_AccessCodeId_TermId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_ContentSectionId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_ExamId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_LessonId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_LessonVideoId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_GrantType_TermId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_UserId_PackageId",
                table: "student_access_grants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_student_access_grants_target_shape",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_recharge_requests_WalletId_Status_Amount_SenderPhoneNumber_~",
                table: "recharge_requests");

            migrationBuilder.DropIndex(
                name: "IX_incoming_sms_logs_MatchedRechargeRequestId",
                table: "incoming_sms_logs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_incoming_sms_logs_match_consistency",
                table: "incoming_sms_logs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_digital_wallets_current_balance_non_negative",
                table: "digital_wallets");

            migrationBuilder.DropIndex(
                name: "IX_balance_transactions_TransactionType_ReferenceId",
                table: "balance_transactions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SecurityStampVersion",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ReservedBalance",
                table: "teacher_accounts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "teacher_accounts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "student_balances");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_PackageId",
                table: "student_access_grants",
                columns: new[] { "UserId", "PackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_WalletId",
                table: "recharge_requests",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_balance_transactions_student_balances_StudentBalanceId",
                table: "balance_transactions",
                column: "StudentBalanceId",
                principalTable: "student_balances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_student_balances_users_UserId",
                table: "student_balances",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_accounts_teacher_profiles_TeacherId",
                table: "teacher_accounts",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_payouts_teacher_profiles_TeacherId",
                table: "teacher_payouts",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
