using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TeacherPlatformFeeDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinancePreset",
                table: "teacher_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql("""
                UPDATE teacher_profiles SET "FinancePreset" = CASE
                    WHEN "Id" IN ('73cf9e05-d068-4a0c-b8e6-dcba16554417', 'ff2b0754-3dcd-4b33-85a9-f5869e0c2768') THEN 1
                    WHEN "Id" = '2a0e7d2f-1dd7-4af0-9974-c999489899b2' THEN 2 ELSE 0 END;

                UPDATE teacher_financial_agreements SET "IsActive" = false,
                    "EffectiveTo" = GREATEST("EffectiveFrom", CURRENT_TIMESTAMP), "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE "IsActive" AND ("ScopeType" IN (1,2,3,4,5)
                    OR ("ScopeType" = 8 AND "ScopeId" IN (SELECT "Id" FROM code_groups WHERE "CodeType" BETWEEN 0 AND 4)));

                INSERT INTO teacher_financial_agreements
                    ("Id", "TeacherId", "ScopeType", "ScopeId", "Trigger", "AllocationMode", "AllocationValue",
                     "PriceBasis", "EffectiveFrom", "EffectiveTo", "IsActive", "Reason", "CreatedByUserId", "CreatedAt")
                SELECT gen_random_uuid(), t."Id", scope, NULL, trigger,
                    CASE WHEN scope IN (1,2) AND t."FinancePreset" <> 2 THEN 0 ELSE 4 END,
                    CASE WHEN scope IN (4,5) THEN CASE WHEN t."FinancePreset" = 1 THEN 12.5 ELSE 15 END
                         WHEN scope = 3 THEN CASE t."FinancePreset" WHEN 1 THEN 50 WHEN 2 THEN 30 ELSE 60 END
                         WHEN t."FinancePreset" = 2 THEN CASE scope WHEN 1 THEN 250 ELSE 100 END
                         ELSE 75 END,
                    1, CURRENT_TIMESTAMP, NULL, true, 'القواعد الافتراضية لنصيب المنصة — 2026-09',
                    '00000000-0000-0000-0000-000000000000'::uuid, CURRENT_TIMESTAMP
                FROM teacher_profiles t CROSS JOIN generate_series(1,5) scope CROSS JOIN generate_series(0,2) trigger
                WHERE NOT (t."FinancePreset" = 2 AND trigger = 1);

                CREATE TEMP TABLE fee_default_unbilled_groups ON COMMIT DROP AS
                SELECT g."Id", t."FinancePreset" FROM code_groups g JOIN teacher_profiles t ON t."Id" = g."TeacherId"
                WHERE g."AccountingRecordedAt" IS NULL AND g."CodeType" BETWEEN 0 AND 4
                    AND NOT EXISTS (SELECT 1 FROM teacher_financial_events e WHERE e."SourceType" = 8 AND e."SourceId" = g."Id")
                    AND (t."FinancePreset" = 2 OR NOT EXISTS (SELECT 1 FROM access_codes c WHERE c."CodeGroupId" = g."Id" AND
                        (c."IsConsumed" OR EXISTS (SELECT 1 FROM teacher_financial_events e WHERE e."SourceType" = 0 AND e."SourceId" = c."Id")
                         OR EXISTS (SELECT 1 FROM access_code_activation_logs l WHERE l."AccessCodeId" = c."Id"))));
                UPDATE code_groups g SET "AccountingTiming" = CASE WHEN u."FinancePreset" = 2 THEN 0 ELSE g."AccountingTiming" END,
                    "RevenueAllocationMode" = NULL, "RevenueAllocationValue" = NULL
                FROM fee_default_unbilled_groups u WHERE g."Id" = u."Id";
                UPDATE code_group_financial_terms f SET "AgreementId" = NULL,
                    "Trigger" = CASE WHEN u."FinancePreset" = 2 THEN 2 ELSE f."Trigger" END
                FROM fee_default_unbilled_groups u WHERE f."CodeGroupId" = u."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException("Financial agreement history requires a reviewed forward correction; it cannot be rolled back by dropping the preset column.");
        }
    }
}
