using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedTeacherCodeCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO financial_accounts ("Id", "Code", "Name", "Type", "NormalSide", "Role", "IsActive", "CreatedAt")
                VALUES (gen_random_uuid(), '1200', 'مبالغ الأكواد المطلوبة من المدرسين', 1, 1, 11, true, CURRENT_TIMESTAMP)
                ON CONFLICT ("Code") DO NOTHING;

                -- Only identical complete source variants are consolidated. Different agreements stay explicit.
                WITH matching AS (
                    SELECT a."TeacherId", a."ScopeType", a."ScopeId", a."AllocationMode", a."AllocationValue", a."PriceBasis",
                        array_agg(a."Id") AS old_ids, (array_agg(a."CreatedByUserId" ORDER BY a."CreatedAt"))[1] AS actor_id
                    FROM teacher_financial_agreements a JOIN teacher_profiles t ON t."Id" = a."TeacherId"
                    WHERE a."IsActive" AND a."EffectiveTo" IS NULL AND a."EffectiveFrom" < CURRENT_TIMESTAMP
                        AND a."Trigger" IN (0, 1, 2) AND a."AllocationMode" <> 3
                        AND NOT EXISTS (SELECT 1 FROM teacher_financial_agreements u
                            WHERE u."TeacherId" = a."TeacherId" AND u."ScopeType" = a."ScopeType"
                                AND u."ScopeId" IS NOT DISTINCT FROM a."ScopeId" AND u."Trigger" = 3 AND u."IsActive")
                    GROUP BY a."TeacherId", a."ScopeType", a."ScopeId", a."AllocationMode", a."AllocationValue", a."PriceBasis", t."FinancePreset"
                    HAVING count(*) = CASE WHEN t."FinancePreset" = 2 THEN 2 ELSE 3 END
                        AND count(DISTINCT a."Trigger") = count(*)
                        AND bool_and(t."FinancePreset" <> 2 OR a."Trigger" IN (0, 2))
                ), inserted AS (
                    INSERT INTO teacher_financial_agreements
                        ("Id", "TeacherId", "ScopeType", "ScopeId", "Trigger", "AllocationMode", "AllocationValue", "PriceBasis",
                         "EffectiveFrom", "IsActive", "Reason", "CreatedByUserId", "CreatedAt")
                    SELECT gen_random_uuid(), "TeacherId", "ScopeType", "ScopeId", 3, "AllocationMode", "AllocationValue", "PriceBasis",
                        CURRENT_TIMESTAMP, true, 'توحيد القواعد المتطابقة للشراء والأكواد — 2026-09-24', actor_id, CURRENT_TIMESTAMP FROM matching
                    RETURNING "Id"
                )
                UPDATE teacher_financial_agreements a SET "EffectiveTo" = CURRENT_TIMESTAMP, "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE a."Id" IN (SELECT unnest(old_ids) FROM matching) AND EXISTS (SELECT 1 FROM inserted);
                """);

            migrationBuilder.AddColumn<bool>(
                name: "RetainedByTeacher",
                table: "teacher_financial_allocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformAmountDue",
                table: "code_group_delivery_confirmations",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TeacherRetainedAmount",
                table: "code_group_delivery_confirmations",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "code_group_delivery_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_group_delivery_payments", x => x.Id);
                    table.CheckConstraint("CK_code_delivery_payment_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_code_group_delivery_payments_code_group_delivery_confirmati~",
                        column: x => x.DeliveryConfirmationId,
                        principalTable: "code_group_delivery_confirmations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_code_group_delivery_payments_financial_journal_entries_Jour~",
                        column: x => x.JournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_code_group_delivery_payments_treasury_accounts_TreasuryAcco~",
                        column: x => x.TreasuryAccountId,
                        principalTable: "treasury_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_code_group_delivery_payments_DeliveryConfirmationId",
                table: "code_group_delivery_payments",
                column: "DeliveryConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_code_group_delivery_payments_IdempotencyKey",
                table: "code_group_delivery_payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_code_group_delivery_payments_JournalEntryId",
                table: "code_group_delivery_payments",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_code_group_delivery_payments_TreasuryAccountId",
                table: "code_group_delivery_payments",
                column: "TreasuryAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM teacher_financial_allocations WHERE "RetainedByTeacher")
                        OR EXISTS (SELECT 1 FROM code_group_delivery_confirmations WHERE "PlatformAmountDue" IS NOT NULL)
                    THEN RAISE EXCEPTION 'Cannot roll back recorded code collections; use a forward repair'; END IF;
                END $$;
                UPDATE teacher_financial_agreements a SET "EffectiveTo" = NULL
                FROM teacher_financial_agreements u
                WHERE u."Reason" = 'توحيد القواعد المتطابقة للشراء والأكواد — 2026-09-24'
                    AND a."TeacherId" = u."TeacherId" AND a."ScopeType" = u."ScopeType"
                    AND a."ScopeId" IS NOT DISTINCT FROM u."ScopeId" AND a."Trigger" IN (0,1,2)
                    AND a."EffectiveTo" = u."EffectiveFrom";
                DELETE FROM teacher_financial_agreements WHERE "Reason" = 'توحيد القواعد المتطابقة للشراء والأكواد — 2026-09-24';
                """);

            migrationBuilder.DropTable(
                name: "code_group_delivery_payments");

            migrationBuilder.DropColumn(
                name: "RetainedByTeacher",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "PlatformAmountDue",
                table: "code_group_delivery_confirmations");

            migrationBuilder.DropColumn(
                name: "TeacherRetainedAmount",
                table: "code_group_delivery_confirmations");
        }
    }
}
