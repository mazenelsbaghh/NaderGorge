using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260821160000_AddAdminAIEntitySearchIndexes")]
public partial class AddAdminAIEntitySearchIndexes : Migration
{
    private static readonly string[] IndexNames =
    [
        "IX_users_admin_ai_normalized_name_trgm",
        "IX_student_profiles_admin_ai_normalized_code_trgm",
        "IX_sag_admin_ai_package_subscribers",
        "IX_sag_admin_ai_term_subscribers",
        "IX_sag_admin_ai_section_subscribers",
        "IX_sag_admin_ai_lesson_subscribers",
        "IX_sag_admin_ai_video_subscribers",
        "IX_sag_admin_ai_video_code_subscribers",
        "IX_sag_admin_ai_exam_subscribers",
        "IX_sag_admin_ai_public_exam_subscribers"
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION massar_normalize_arabic(value text)
            RETURNS text
            LANGUAGE plpgsql
            IMMUTABLE
            PARALLEL SAFE
            STRICT
            AS $function$
            DECLARE
                character text;
                codepoint integer;
                normalized text := '';
            BEGIN
                FOREACH character IN ARRAY regexp_split_to_array(lower(normalize(value, NFKC)), '')
                LOOP
                    codepoint := ascii(character);
                    IF character = 'ـ'
                       OR codepoint BETWEEN 1552 AND 1562
                       OR codepoint BETWEEN 1611 AND 1631
                       OR codepoint = 1648
                       OR codepoint BETWEEN 1750 AND 1756
                       OR codepoint BETWEEN 1759 AND 1764
                       OR codepoint BETWEEN 1767 AND 1768
                       OR codepoint BETWEEN 1770 AND 1773
                       OR codepoint BETWEEN 2259 AND 2273
                       OR codepoint BETWEEN 2275 AND 2303 THEN
                        CONTINUE;
                    END IF;
                    normalized := normalized || CASE character
                        WHEN 'آ' THEN 'ا'
                        WHEN 'أ' THEN 'ا'
                        WHEN 'إ' THEN 'ا'
                        WHEN 'ى' THEN 'ي'
                        ELSE character
                    END;
                END LOOP;
                RETURN btrim(regexp_replace(normalized, '\s+', ' ', 'g'));
            END;
            $function$;
            """,
            suppressTransaction: true);

        foreach (var indexName in IndexNames)
            migrationBuilder.Sql($"DROP INDEX CONCURRENTLY IF EXISTS \"{indexName}\";", suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_users_admin_ai_normalized_name_trgm"
                ON users USING gin (massar_normalize_arabic("FullName") public.gin_trgm_ops)
                WHERE "IsDeleted" = FALSE;
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_student_profiles_admin_ai_normalized_code_trgm"
                ON student_profiles USING gin (massar_normalize_arabic("StudentCode") public.gin_trgm_ops)
                WHERE "StudentCode" IS NOT NULL;
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_package_subscribers"
                ON student_access_grants ("PackageId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId")
                WHERE "CancelledAt" IS NULL
                  AND "PackageId" IS NOT NULL
                  AND ("GrantType" = 0 OR ("GrantType" = 4 AND "VideoTypeId" IS NOT NULL));
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_term_subscribers"
                ON student_access_grants ("TermId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId")
                WHERE "CancelledAt" IS NULL
                  AND "TermId" IS NOT NULL
                  AND ("GrantType" = 1 OR ("GrantType" = 4 AND "VideoTypeId" IS NOT NULL));
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_section_subscribers"
                ON student_access_grants ("ContentSectionId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId")
                WHERE "CancelledAt" IS NULL
                  AND "ContentSectionId" IS NOT NULL
                  AND ("GrantType" = 2 OR ("GrantType" = 4 AND "VideoTypeId" IS NOT NULL));
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_lesson_subscribers"
                ON student_access_grants ("LessonId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId")
                WHERE "CancelledAt" IS NULL
                  AND "LessonId" IS NOT NULL
                  AND ("GrantType" = 3 OR ("GrantType" = 4 AND "VideoTypeId" IS NOT NULL));
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_video_subscribers"
                ON student_access_grants ("LessonVideoId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId", "MaxUses", "UsesConsumed")
                WHERE "CancelledAt" IS NULL AND "GrantType" = 4 AND "LessonVideoId" IS NOT NULL;
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_video_code_subscribers"
                ON student_access_grants ("AccessCodeId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId", "MaxUses", "UsesConsumed")
                WHERE "CancelledAt" IS NULL
                  AND "GrantType" = 4
                  AND "VideoTypeId" IS NOT NULL
                  AND "AccessCodeId" IS NOT NULL;
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_exam_subscribers"
                ON student_access_grants ("ExamId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId", "PublicExamProductId")
                WHERE "CancelledAt" IS NULL AND "GrantType" = 5 AND "ExamId" IS NOT NULL;
            """,
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY "IX_sag_admin_ai_public_exam_subscribers"
                ON student_access_grants ("PublicExamProductId", "UserId")
                INCLUDE ("IsActive", "ExpiresAt", "GiftRecipientId", "ExamId")
                WHERE "CancelledAt" IS NULL AND "GrantType" = 5 AND "PublicExamProductId" IS NOT NULL;
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var indexName in IndexNames)
            migrationBuilder.Sql($"DROP INDEX CONCURRENTLY IF EXISTS \"{indexName}\";", suppressTransaction: true);
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS massar_normalize_arabic(text);", suppressTransaction: true);
    }
}
