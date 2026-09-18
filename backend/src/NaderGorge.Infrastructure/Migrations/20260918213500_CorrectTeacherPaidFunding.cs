using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260918213500_CorrectTeacherPaidFunding")]
public sealed class CorrectTeacherPaidFunding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- The September 18 incident classified paid teacher-scoped recharge as a gift.
            -- Preserve source events and journals; append corrections using their recorded fee.
            SET LOCAL lock_timeout = '10s';
            LOCK TABLE teacher_accounts, teacher_financial_events, teacher_financial_allocations,
                sales_financial_effects, financial_journal_entries, financial_journal_lines,
                student_access_grants, platform_refunds, promotional_balance_usages,
                promotional_balance_allocations, gift_recipients, accounting_periods, financial_accounts
                IN SHARE ROW EXCLUSIVE MODE;

            CREATE TEMP TABLE teacher_funding_corrections ON COMMIT DROP AS
            WITH paid AS (
                SELECT u."PurchaseOperationId", sum(u."Amount") amount
                FROM promotional_balance_usages u
                JOIN promotional_balance_allocations p ON p."Id" = u."AllocationId"
                JOIN gift_recipients r ON r."Id" = p."GiftRecipientId"
                WHERE r."OutcomeCode" = 'DIGITAL_RECHARGE'
                    AND p."TeacherId" = '2a0e7d2f-1dd7-4af0-9974-c999489899b2'
                GROUP BY u."PurchaseOperationId"
            )
            SELECT a."Id" allocation_id, e."Id" event_id, s."Id" sale_id, j."Id" original_journal_id,
                e."SourceId" purchase_id, a."TeacherId" teacher_id, e."StudentId" student_id,
                p.amount scoped_paid,
                greatest(e."PaidAmount" + p.amount - a."AllocationValue", 0) - a."TeacherShareAmount" teacher_delta,
                p.amount - (greatest(e."PaidAmount" + p.amount - a."AllocationValue", 0) - a."TeacherShareAmount") platform_delta,
                gen_random_uuid() correction_id, gen_random_uuid() journal_id,
                'teacher-paid-funding-20260919:' || a."Id"::text correction_key
            FROM teacher_financial_allocations a
            JOIN teacher_financial_events e ON e."Id" = a."TeacherFinancialEventId"
            JOIN paid p ON p."PurchaseOperationId"::text = e."DetailsJson"::jsonb->>'fundingOperationId'
            JOIN sales_financial_effects s ON s."PurchaseOperationId" = e."SourceId"
            JOIN financial_journal_entries j ON j."SourceId" = e."SourceId"
                AND j."IdempotencyKey" = 'purchase:' || replace(e."SourceId"::text, '-', '')
            WHERE a."TeacherId" = '2a0e7d2f-1dd7-4af0-9974-c999489899b2'
                AND e."SourceType" = 1 AND e."TargetType" BETWEEN 0 AND 3 AND e."Currency" = 'EGP'
                AND e."OccurredAt" < timestamp '2026-09-18 21:18:00'
                AND NOT (e."DetailsJson"::jsonb ? 'paidTeacherBalanceAmount')
                AND NOT (s."DetailsJson"::jsonb ? 'paidTeacherBalanceAmount')
                AND a."AgreementAllocationMode" = 4 AND a."PriceBasis" = 1
                AND a."AgreementId" IS NOT NULL AND a."AllocationValue" > 0
                AND a."ReviewStatus" IN (0,2) AND e."ReviewStatus" IN (0,2)
                AND a."PayoutStatus" IN (0,1) AND a."ReversedAmount" = 0
                AND a."SettlementLineId" IS NULL AND a."PayoutId" IS NULL
                AND a."GrossBasisAmount" = e."PaidAmount"
                AND a."TeacherShareAmount" = greatest(e."PaidAmount" - a."AllocationValue", 0)
                AND a."PlatformShareAmount" = e."PaidAmount" - a."TeacherShareAmount"
                AND e."PlatformShareAmount" = a."PlatformShareAmount"
                AND p.amount > 0 AND e."PromotionalAmount" = p.amount
                AND s."PaidAmount" = e."PaidAmount" AND s."PromotionalAmount" = p.amount
                AND s."TeacherShareImpact" = a."TeacherShareAmount" AND s."PlatformShareImpact" = a."PlatformShareAmount"
                AND s."TeacherId" = a."TeacherId" AND s."StudentId" = e."StudentId"
                AND s."TargetType" = e."TargetType" AND s."TargetId" = e."TargetId"
                AND (NOT (s."DetailsJson"::jsonb ? 'fundingOperationId')
                    OR s."DetailsJson"::jsonb->>'fundingOperationId' = p."PurchaseOperationId"::text)
                AND (SELECT count(*) FROM teacher_financial_allocations other WHERE other."TeacherFinancialEventId" = e."Id") = 1
                AND j."Status" = 1 AND j."ReversalOfId" IS NULL
                AND (SELECT count(*) FROM financial_journal_entries other WHERE other."SourceId" = e."SourceId") = 1
                AND NOT EXISTS (SELECT 1 FROM platform_refunds r WHERE r."OriginalSourceId" = e."SourceId")
                AND NOT EXISTS (SELECT 1 FROM teacher_financial_events c WHERE c."IdempotencyKey" = 'teacher-paid-funding-20260919:' || a."Id"::text)
                AND (SELECT count(*) FROM student_access_grants g WHERE g."UserId" = e."StudentId"
                    AND g."GrantType" = e."TargetType"
                    AND CASE e."TargetType" WHEN 0 THEN g."PackageId" WHEN 1 THEN g."TermId"
                        WHEN 2 THEN g."ContentSectionId" WHEN 3 THEN g."LessonId" END = e."TargetId"
                    AND abs(extract(epoch FROM g."GrantedAt" - e."OccurredAt")) < 60
                    AND g."IsActive" AND g."CancelledAt" IS NULL) = 1
                AND NOT EXISTS (SELECT 1 FROM student_access_grants g WHERE g."UserId" = e."StudentId"
                    AND g."GrantType" = e."TargetType"
                    AND CASE e."TargetType" WHEN 0 THEN g."PackageId" WHEN 1 THEN g."TermId"
                        WHEN 2 THEN g."ContentSectionId" WHEN 3 THEN g."LessonId" END = e."TargetId"
                    AND g."CancelledAt" >= e."OccurredAt")
                AND NOT EXISTS (SELECT 1 FROM teacher_financial_events r WHERE r."StudentId" = e."StudentId"
                    AND r."TargetType" = e."TargetType" AND r."TargetId" = e."TargetId"
                    AND r."SourceType" IN (4,5) AND r."OccurredAt" >= e."OccurredAt");

            -- Require the exact old posting shape. Unknown journals are not silently rewritten.
            DELETE FROM teacher_funding_corrections c
            WHERE EXISTS (
                SELECT 1 FROM financial_journal_lines l JOIN financial_accounts f ON f."Id" = l."FinancialAccountId"
                WHERE l."JournalEntryId" = c.original_journal_id
                    AND (f."Code" NOT IN ('1100','1110','2000','4000') OR l."StudentId" IS DISTINCT FROM c.student_id
                    OR (f."Code" IN ('1110','2000') AND l."TeacherId" IS DISTINCT FROM c.teacher_id))
            ) OR EXISTS (
                SELECT 1 FROM (VALUES ('1100'),('1110'),('2000'),('4000')) code(value)
                JOIN teacher_financial_events e ON e."Id" = c.event_id
                JOIN teacher_financial_allocations a ON a."Id" = c.allocation_id
                WHERE (SELECT coalesce(sum(l."Debit" - l."Credit"),0)
                    FROM financial_journal_lines l JOIN financial_accounts f ON f."Id" = l."FinancialAccountId"
                    WHERE l."JournalEntryId" = c.original_journal_id AND f."Code" = code.value)
                    <> CASE code.value WHEN '1100' THEN e."PaidAmount" WHEN '1110' THEN c.scoped_paid
                        WHEN '2000' THEN -a."TeacherShareAmount" - c.scoped_paid ELSE -a."PlatformShareAmount" END
            );

            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM teacher_funding_corrections) THEN
                    IF EXISTS (SELECT 1 FROM accounting_periods WHERE "StartDate" <= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date
                        AND "EndDate" >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date AND "Status" = 2) THEN
                        RAISE EXCEPTION 'Teacher funding correction requires an open accounting period';
                    END IF;
                    IF (SELECT count(*) FROM financial_accounts WHERE "Code" IN ('2000','4000') AND "IsActive") <> 2
                        OR NOT EXISTS (SELECT 1 FROM teacher_accounts WHERE "TeacherId" = '2a0e7d2f-1dd7-4af0-9974-c999489899b2')
                        OR EXISTS (SELECT 1 FROM teacher_funding_corrections WHERE teacher_delta < 0 OR platform_delta < 0) THEN
                        RAISE EXCEPTION 'Teacher funding correction prerequisites failed';
                    END IF;
                END IF;
            END $$;

            INSERT INTO teacher_financial_events
                ("Id","SourceType","SourceId","StudentId","TargetType","TargetId","GrossAmount","DiscountAmount",
                 "PlatformDiscountAmount","TeacherDiscountAmount","PaidAmount","PromotionalAmount","PlatformShareAmount",
                 "Currency","ReviewStatus","PayoutStatus","OccurredAt","IdempotencyKey","DetailsJson","CreatedAt")
            SELECT c.correction_id,7,e."SourceId",e."StudentId",e."TargetType",e."TargetId",0,0,0,0,
                c.scoped_paid,-c.scoped_paid,c.platform_delta,'EGP',0,CASE WHEN c.teacher_delta > 0 THEN 1 ELSE 0 END,
                CURRENT_TIMESTAMP AT TIME ZONE 'UTC',c.correction_key,
                jsonb_build_object('reason','تصحيح تصنيف شحن مدفوع ضمن رصيد الهدايا — دون تغيير الاتفاق التاريخي',
                    'originalEventId',e."Id",'originalAllocationId',a."Id",'originalJournalId',c.original_journal_id,
                    'paidBefore',e."PaidAmount",'teacherShareBefore',a."TeacherShareAmount",'scopedPaid',c.scoped_paid,
                    'teacherShareCorrection',c.teacher_delta,'platformShareCorrection',c.platform_delta),
                CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM teacher_funding_corrections c JOIN teacher_financial_events e ON e."Id" = c.event_id
            JOIN teacher_financial_allocations a ON a."Id" = c.allocation_id;

            INSERT INTO teacher_financial_allocations
                ("Id","TeacherFinancialEventId","TeacherId","AllocationMode","AllocationValue","GrossBasisAmount",
                 "TeacherShareAmount","PlatformShareAmount","AgreementId","AgreementScopeType","AgreementScopeId",
                 "AgreementAllocationMode","PriceBasis","DiscountBearer","ReversedAmount","StudentNameSnapshot",
                 "StudentPhoneSnapshot","ContentNameSnapshot","ReviewStatus","PayoutStatus","CreatedAt")
            SELECT gen_random_uuid(),c.correction_id,a."TeacherId",3,c.teacher_delta,c.scoped_paid,
                c.teacher_delta,c.platform_delta,a."AgreementId",a."AgreementScopeType",a."AgreementScopeId",
                a."AgreementAllocationMode",a."PriceBasis",a."DiscountBearer",0,a."StudentNameSnapshot",
                a."StudentPhoneSnapshot",a."ContentNameSnapshot",0,CASE WHEN c.teacher_delta > 0 THEN 1 ELSE 0 END,
                CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM teacher_funding_corrections c JOIN teacher_financial_allocations a ON a."Id" = c.allocation_id;

            -- The original journal already credited the teacher with all scoped cash.
            -- Transfer only the missing platform fee; do not consume student funds again.
            INSERT INTO financial_journal_entries
                ("Id","SequenceNumber","OccurredAt","PostedAt","SourceType","SourceId","PostingKind",
                 "IdempotencyKey","Description","Status","CreatedAt")
            SELECT c.journal_id,(SELECT coalesce(max("SequenceNumber"),0) FROM financial_journal_entries)
                + row_number() OVER (ORDER BY c.allocation_id),CURRENT_TIMESTAMP AT TIME ZONE 'UTC',CURRENT_TIMESTAMP AT TIME ZONE 'UTC',
                'TeacherFundingCorrection',c.purchase_id,'TeacherPaidFundingCorrection',c.correction_key,
                'تصحيح حصة المنصة من رصيد شحن المدرس المدفوع',1,CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM teacher_funding_corrections c WHERE c.platform_delta > 0;

            INSERT INTO financial_journal_lines
                ("Id","JournalEntryId","FinancialAccountId","Debit","Credit","StudentId","TeacherId","CreatedAt")
            SELECT gen_random_uuid(),c.journal_id,f."Id",
                CASE WHEN f."Code" = '2000' THEN c.platform_delta ELSE 0 END,
                CASE WHEN f."Code" = '4000' THEN c.platform_delta ELSE 0 END,
                c.student_id,CASE WHEN f."Code" = '2000' THEN c.teacher_id END,CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM teacher_funding_corrections c CROSS JOIN financial_accounts f
            WHERE c.platform_delta > 0 AND f."Code" IN ('2000','4000');

            UPDATE sales_financial_effects s SET
                "PaidAmount" = s."PaidAmount" + c.scoped_paid,"PromotionalAmount" = s."PromotionalAmount" - c.scoped_paid,
                "TeacherShareImpact" = s."TeacherShareImpact" + c.teacher_delta,
                "PlatformShareImpact" = s."PlatformShareImpact" + c.platform_delta,
                "DetailsJson" = (s."DetailsJson"::jsonb || jsonb_build_object('paidTeacherBalanceAmount',c.scoped_paid,
                    'fundingCorrectionEventId',c.correction_id)),"UpdatedAt" = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM teacher_funding_corrections c WHERE s."Id" = c.sale_id;

            UPDATE teacher_accounts a SET "TotalEarnings" = a."TotalEarnings" + c.amount,
                "CurrentBalance" = a."CurrentBalance" + c.amount,"Version" = a."Version" + 1,"UpdatedAt" = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM (SELECT teacher_id,sum(teacher_delta) amount FROM teacher_funding_corrections GROUP BY teacher_id) c
            WHERE a."TeacherId" = c.teacher_id;

            DROP TABLE teacher_funding_corrections;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Financial corrections require a reviewed forward reversal.");
}
