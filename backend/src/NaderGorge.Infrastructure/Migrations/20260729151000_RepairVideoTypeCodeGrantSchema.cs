using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260729151000_RepairVideoTypeCodeGrantSchema")]
public partial class RepairVideoTypeCodeGrantSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL lock_timeout = '5s';

            ALTER TABLE users
                ADD COLUMN IF NOT EXISTS "SecurityStampVersion" integer NOT NULL DEFAULT 0;

            ALTER TABLE student_access_grants
                ADD COLUMN IF NOT EXISTS "VideoTypeId" uuid NULL;

            ALTER TABLE code_groups
                ADD COLUMN IF NOT EXISTS "VideoTypeId" uuid NULL;

            ALTER TABLE student_access_grants
                DROP CONSTRAINT IF EXISTS "CK_student_access_grants_target_shape";

            ALTER TABLE student_access_grants
                ADD CONSTRAINT "CK_student_access_grants_target_shape"
                CHECK (
                    ("GrantType" = 0 AND "PackageId" IS NOT NULL AND "TermId" IS NULL AND "ContentSectionId" IS NULL AND "LessonId" IS NULL AND "LessonVideoId" IS NULL AND "ExamId" IS NULL) OR
                    ("GrantType" = 1 AND "TermId" IS NOT NULL AND "ContentSectionId" IS NULL AND "LessonId" IS NULL AND "LessonVideoId" IS NULL AND "ExamId" IS NULL) OR
                    ("GrantType" = 2 AND "ContentSectionId" IS NOT NULL AND "LessonId" IS NULL AND "LessonVideoId" IS NULL AND "ExamId" IS NULL) OR
                    ("GrantType" = 3 AND "LessonId" IS NOT NULL AND "LessonVideoId" IS NULL AND "ExamId" IS NULL) OR
                    ("GrantType" = 4 AND ("LessonVideoId" IS NOT NULL OR "VideoTypeId" IS NOT NULL) AND "ExamId" IS NULL) OR
                    ("GrantType" = 5 AND "ExamId" IS NOT NULL AND "LessonVideoId" IS NULL)
                ) NOT VALID;

            UPDATE roles
            SET "PermissionsJson" = '["users.manage","watch_requests.manage","community.manage","comments.manage","tasks.manage","chat.manage","crm.manage","payments.manage","reports.manage"]',
                "AllowedDomain" = 'assistant'
            WHERE "Name" = 'Staff';
            """);

        migrationBuilder.Sql("""
            SET lock_timeout = '5s';
            SET statement_timeout = '30min';
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            ALTER TABLE student_access_grants
                VALIDATE CONSTRAINT "CK_student_access_grants_target_shape";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            RESET statement_timeout;
            RESET lock_timeout;
            """, suppressTransaction: true);

        migrationBuilder.Sql("""
            SET lock_timeout = '5s';
            SET statement_timeout = '30min';
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            DROP INDEX CONCURRENTLY IF EXISTS "IX_code_groups_VideoTypeId";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_code_groups_VideoTypeId"
                ON code_groups ("VideoTypeId");
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            RESET statement_timeout;
            RESET lock_timeout;
            """, suppressTransaction: true);

        migrationBuilder.Sql("""
            SET lock_timeout = '5s';
            SET statement_timeout = '30min';
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            DROP INDEX CONCURRENTLY IF EXISTS
                "IX_student_access_grants_video_type_scope";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_student_access_grants_video_type_scope"
                ON student_access_grants ("UserId", "GrantType", "VideoTypeId", "PackageId", "TermId", "ContentSectionId", "LessonId")
                WHERE "IsActive" = TRUE AND "GrantType" = 4 AND "VideoTypeId" IS NOT NULL;
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            RESET statement_timeout;
            RESET lock_timeout;
            """, suppressTransaction: true);

        migrationBuilder.Sql("""
            SET lock_timeout = '5s';
            SET statement_timeout = '30min';
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            DROP INDEX CONCURRENTLY IF EXISTS
                "IX_teacher_staff_members_UserId_online";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_teacher_staff_members_UserId_online"
                ON teacher_staff_members ("UserId");
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            DROP INDEX CONCURRENTLY IF EXISTS
                "IX_teacher_staff_members_UserId";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            ALTER INDEX "IX_teacher_staff_members_UserId_online"
                RENAME TO "IX_teacher_staff_members_UserId";
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            RESET statement_timeout;
            RESET lock_timeout;
            """, suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
