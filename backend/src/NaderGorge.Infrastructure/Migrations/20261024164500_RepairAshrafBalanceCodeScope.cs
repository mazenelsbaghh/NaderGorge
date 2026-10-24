using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261024164500_RepairAshrafBalanceCodeScope")]
public sealed class RepairAshrafBalanceCodeScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL lock_timeout = '5s';
            DO $repair$
            DECLARE
                group_id constant uuid := 'b134dca5-5ddb-4b01-a061-e9a56b6ac23a';
                teacher_id constant uuid := '73cf9e05-d068-4a0c-b8e6-dcba16554417';
                repair_key constant text := 'ashraf-code-scope-20261024';
                batch code_groups%ROWTYPE;
                student record;
                issuance_id uuid;
                recipient_id uuid;
                moved numeric := 0;
            BEGIN
                SELECT * INTO batch FROM code_groups WHERE "Id" = group_id FOR UPDATE;
                IF NOT FOUND THEN RETURN; END IF;

                -- The group lock serializes replays; the audit marker commits with every movement.
                IF EXISTS (SELECT 1 FROM audit_logs WHERE "Action" = 'RepairCodeBalanceScope'
                    AND "EntityId" = group_id AND "RequestId" = repair_key) THEN
                    IF batch."TeacherId" IS DISTINCT FROM teacher_id THEN
                        RAISE EXCEPTION 'Code scope repair marker conflicts with current ownership';
                    END IF;
                    RETURN;
                END IF;

                IF batch."TeacherId" IS NOT NULL OR batch."CodeType" <> 6
                    OR batch."BalanceAmount" IS DISTINCT FROM 850::numeric
                    OR batch."Name" <> 'اكواد اشرف بكر ب 850'
                    OR batch."ExpiresAt" IS NOT NULL THEN
                    RAISE EXCEPTION 'Code scope repair requires the reviewed unassigned balance batch';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM teacher_profiles WHERE "Id" = teacher_id) THEN
                    RAISE EXCEPTION 'Code scope repair teacher is missing';
                END IF;

                PERFORM "Id" FROM access_codes WHERE "CodeGroupId" = group_id ORDER BY "Id" FOR UPDATE;
                PERFORM b."Id" FROM student_balances b
                    WHERE b."UserId" IN (SELECT "ConsumedByUserId" FROM access_codes
                        WHERE "CodeGroupId" = group_id AND "IsConsumed")
                    ORDER BY b."Id" FOR UPDATE;

                -- Additional spending may reduce the remainder; new funding or refunds require a fresh review.
                IF (SELECT count(*) FROM access_codes WHERE "CodeGroupId" = group_id) <> 100
                    OR (SELECT count(*) FROM access_codes WHERE "CodeGroupId" = group_id AND "IsConsumed") <> 55
                    OR (SELECT count(DISTINCT "ConsumedByUserId") FROM access_codes
                        WHERE "CodeGroupId" = group_id AND "IsConsumed") <> 55 THEN
                    RAISE EXCEPTION 'Code scope repair usage changed; refresh the 55-student review';
                END IF;

                CREATE TEMP TABLE ashraf_scope_review ON COMMIT DROP AS
                SELECT c."Id" code_id, b."Id" balance_id, b."UserId" student_id,
                    b."CurrentBalance" remaining,
                    count(*) FILTER (WHERE t."TransactionType" = 'CodeRedemption'
                        AND t."ReferenceId" = c."Id" AND t."Amount" = 850) source_credits,
                    coalesce(sum(t."Amount"), 0) ledger_balance,
                    coalesce(sum(-t."Amount") FILTER (WHERE t."Amount" < 0), 0) debits,
                    coalesce(sum(t."Amount") FILTER (WHERE t."TransactionType" = 'Refund'), 0) refunds,
                    count(*) FILTER (WHERE t."Amount" > 0 AND
                        (t."TransactionType" = 'Refund' OR (t."TransactionType" = 'CodeRedemption'
                            AND t."ReferenceId" = c."Id" AND t."Amount" = 850)) IS NOT TRUE) other_credits
                FROM access_codes c
                JOIN student_balances b ON b."UserId" = c."ConsumedByUserId"
                LEFT JOIN balance_transactions t ON t."StudentBalanceId" = b."Id"
                WHERE c."CodeGroupId" = group_id AND c."IsConsumed"
                GROUP BY c."Id", b."Id", b."UserId", b."CurrentBalance";

                IF (SELECT count(*) FROM ashraf_scope_review) <> 55
                    OR EXISTS (SELECT 1 FROM ashraf_scope_review
                        WHERE source_credits <> 1 OR other_credits <> 0 OR ledger_balance <> remaining
                            OR remaining < 0 OR remaining > 850)
                    OR (SELECT count(*) FROM ashraf_scope_review WHERE remaining > 0) > 3
                    OR (SELECT sum(remaining) FROM ashraf_scope_review) > 2550
                    OR (SELECT sum(debits) FROM ashraf_scope_review) < 44500
                    OR (SELECT sum(refunds) FROM ashraf_scope_review) <> 300 THEN
                    RAISE EXCEPTION 'Code scope repair ledger changed; no funds were moved';
                END IF;

                IF EXISTS (SELECT 1 FROM gift_issuances g JOIN ashraf_scope_review r
                    ON g."RequestId" = r.code_id) THEN
                    RAISE EXCEPTION 'Code scope repair source already has a scoped issuance';
                END IF;

                FOR student IN SELECT * FROM ashraf_scope_review WHERE remaining > 0 ORDER BY balance_id LOOP
                    issuance_id := gen_random_uuid();
                    recipient_id := gen_random_uuid();
                    UPDATE student_balances SET "CurrentBalance" = "CurrentBalance" - student.remaining,
                        "Version" = "Version" + 1, "UpdatedAt" = CURRENT_TIMESTAMP
                    WHERE "Id" = student.balance_id;

                    INSERT INTO balance_transactions
                        ("Id", "StudentBalanceId", "Amount", "BalanceAfter", "TransactionType",
                         "ReferenceId", "Description", "CreatedAt")
                    VALUES (gen_random_uuid(), student.balance_id, -student.remaining, 0,
                        'CodeScopeCorrection', student.code_id,
                        'نقل الرصيد المتبقي من دفعة الأكواد إلى رصيد المدرس — ' || repair_key, CURRENT_TIMESTAMP);

                    INSERT INTO gift_issuances
                        ("Id", "RequestId", "TargetType", "TeacherId", "Amount", "Reason",
                         "IssuedByUserId", "Status", "CreatedAt")
                    VALUES (issuance_id, student.code_id, 5, teacher_id, student.remaining,
                        'Teacher scoped balance code: ' || batch."Name" || ' [' || repair_key || ']',
                        batch."CreatedByUserId", 0, CURRENT_TIMESTAMP);
                    INSERT INTO gift_recipients
                        ("Id", "GiftIssuanceId", "StudentId", "Status", "OutcomeCode",
                         "OutcomeMessage", "UsesConsumed", "CreatedAt")
                    VALUES (recipient_id, issuance_id, student.student_id, 3, 'GRANTED',
                        'تصحيح نطاق الرصيد المتبقي من كود المدرس بدون شحن إضافي', 0, CURRENT_TIMESTAMP);
                    INSERT INTO promotional_balance_allocations
                        ("Id", "GiftRecipientId", "StudentId", "TeacherId", "OriginalAmount", "AvailableAmount",
                         "ConsumedAmount", "ExpiredAmount", "RevokedAmount", "PurchaseCount", "Status", "CreatedAt")
                    VALUES (gen_random_uuid(), recipient_id, student.student_id, teacher_id,
                        student.remaining, student.remaining, 0, 0, 0, 0, 0, CURRENT_TIMESTAMP);

                    INSERT INTO outbox_events
                        ("Id", "Type", "TargetUserId", "PayloadJson", "RetryCount", "IsDeadLetter", "CreatedAt")
                    VALUES (gen_random_uuid(), 'BalanceChanged', student.student_id::text,
                        jsonb_build_object('currentBalance', 0, 'scopedTeacherId', teacher_id,
                            'scopedTeacherName', 'أشرف بكر', 'promotionalAmount', student.remaining,
                            'repairReference', repair_key)::text, 0, false, CURRENT_TIMESTAMP);
                    moved := moved + student.remaining;
                END LOOP;

                IF moved > 2550 THEN RAISE EXCEPTION 'Code scope repair conservation failed'; END IF;
                UPDATE code_groups SET "TeacherId" = teacher_id, "UpdatedAt" = CURRENT_TIMESTAMP WHERE "Id" = group_id;
                INSERT INTO audit_logs
                    ("Id", "Action", "EntityType", "EntityId", "ActorType", "RequestId", "Reason",
                     "OldValues", "NewValues", "CreatedAt")
                VALUES (gen_random_uuid(), 'RepairCodeBalanceScope', 'CodeGroup', group_id, 'System', repair_key,
                    'Owner-authorized correction; reviewed 55 redemptions; preserve historical spending',
                    jsonb_build_object('teacherId', NULL, 'generalBalanceToMove', moved)::text,
                    jsonb_build_object('teacherId', teacher_id, 'teacherBalanceMoved', moved,
                        'reviewedCodes', 55, 'unchangedSpentCodes',
                        (SELECT count(*) FROM ashraf_scope_review WHERE remaining = 0), 'unusedCodes', 45)::text, CURRENT_TIMESTAMP);
                DROP TABLE ashraf_scope_review;
            END $repair$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Use a reviewed forward correction; scoped balances may already have been spent.");
}
