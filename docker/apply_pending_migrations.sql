-- ==========================================================
-- Apply all pending EF Core migrations manually
-- From: 20260408164959 to 20260409173000
-- Each migration in its own transaction for safety
-- ==========================================================

-- ============================================================
-- Migration 1: 20260408164959_AddLessonCommentsModeration
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS "lesson_comments" (
    "Id" uuid NOT NULL,
    "LessonId" uuid NOT NULL,
    "AuthorUserId" uuid NOT NULL,
    "Body" character varying(2000) NOT NULL,
    "Status" integer NOT NULL,
    "ReviewedAt" timestamp without time zone,
    "ReviewedByUserId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    CONSTRAINT "PK_lesson_comments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_lesson_comments_lessons_LessonId" FOREIGN KEY ("LessonId") REFERENCES "lessons" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_lesson_comments_users_AuthorUserId" FOREIGN KEY ("AuthorUserId") REFERENCES "users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_lesson_comments_users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES "users" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_lesson_comments_AuthorUserId" ON "lesson_comments" ("AuthorUserId");
CREATE INDEX IF NOT EXISTS "IX_lesson_comments_CreatedAt" ON "lesson_comments" ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_lesson_comments_LessonId" ON "lesson_comments" ("LessonId");
CREATE INDEX IF NOT EXISTS "IX_lesson_comments_ReviewedByUserId" ON "lesson_comments" ("ReviewedByUserId");
CREATE INDEX IF NOT EXISTS "IX_lesson_comments_Status" ON "lesson_comments" ("Status");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260408164959_AddLessonCommentsModeration', '9.0.3'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260408164959_AddLessonCommentsModeration');

COMMIT;

-- ============================================================
-- Migration 2: 20260408171549_AddStudentCommunity
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS "community_posts" (
    "Id" uuid NOT NULL,
    "AuthorUserId" uuid NOT NULL,
    "Body" character varying(4000) NOT NULL,
    "Status" integer NOT NULL,
    "ReviewedAt" timestamp without time zone,
    "ReviewedByUserId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    CONSTRAINT "PK_community_posts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_community_posts_users_AuthorUserId" FOREIGN KEY ("AuthorUserId") REFERENCES "users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_community_posts_users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES "users" ("Id") ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS "community_post_comments" (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "AuthorUserId" uuid NOT NULL,
    "Body" character varying(2000) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    CONSTRAINT "PK_community_post_comments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_community_post_comments_community_posts_PostId" FOREIGN KEY ("PostId") REFERENCES "community_posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_community_post_comments_users_AuthorUserId" FOREIGN KEY ("AuthorUserId") REFERENCES "users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "community_post_likes" (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    CONSTRAINT "PK_community_post_likes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_community_post_likes_community_posts_PostId" FOREIGN KEY ("PostId") REFERENCES "community_posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_community_post_likes_users_UserId" FOREIGN KEY ("UserId") REFERENCES "users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_community_post_comments_AuthorUserId" ON "community_post_comments" ("AuthorUserId");
CREATE INDEX IF NOT EXISTS "IX_community_post_comments_CreatedAt" ON "community_post_comments" ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_community_post_comments_PostId" ON "community_post_comments" ("PostId");
CREATE INDEX IF NOT EXISTS "IX_community_post_likes_PostId" ON "community_post_likes" ("PostId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_community_post_likes_PostId_UserId" ON "community_post_likes" ("PostId", "UserId");
CREATE INDEX IF NOT EXISTS "IX_community_post_likes_UserId" ON "community_post_likes" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_community_posts_AuthorUserId" ON "community_posts" ("AuthorUserId");
CREATE INDEX IF NOT EXISTS "IX_community_posts_CreatedAt" ON "community_posts" ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_community_posts_ReviewedByUserId" ON "community_posts" ("ReviewedByUserId");
CREATE INDEX IF NOT EXISTS "IX_community_posts_Status" ON "community_posts" ("Status");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260408171549_AddStudentCommunity', '9.0.3'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260408171549_AddStudentCommunity');

COMMIT;

-- ============================================================
-- Migration 3: 20260408175220_AddStudentThemePreferences
-- ============================================================
BEGIN;

ALTER TABLE "student_profiles" ADD COLUMN IF NOT EXISTS "DarkThemePaletteId" character varying(100);
ALTER TABLE "student_profiles" ADD COLUMN IF NOT EXISTS "LightThemePaletteId" character varying(100);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260408175220_AddStudentThemePreferences', '9.0.3'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260408175220_AddStudentThemePreferences');

COMMIT;

-- ============================================================
-- Migration 4: 20260408190000_AddPackageCodePageProfiles
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS "package_code_page_profiles" (
    "Id" uuid NOT NULL,
    "PackageId" uuid NOT NULL,
    "Status" integer NOT NULL,
    "HeroEyebrow" character varying(80),
    "HeroTitle" character varying(140),
    "HeroDescription" character varying(600),
    "OfferTitle" character varying(120),
    "OfferDescription" character varying(600),
    "ActivationTitle" character varying(120),
    "ActivationDescription" character varying(500),
    "SupportTitle" character varying(120),
    "SupportDescription" character varying(400),
    "ThemeAccentKey" character varying(60),
    "UpdatedByUserId" uuid,
    "PublishedAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    CONSTRAINT "PK_package_code_page_profiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_package_code_page_profiles_packages_PackageId" FOREIGN KEY ("PackageId") REFERENCES "packages" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_package_code_page_profiles_users_UpdatedByUserId" FOREIGN KEY ("UpdatedByUserId") REFERENCES "users" ("Id") ON DELETE SET NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_package_code_page_profiles_PackageId" ON "package_code_page_profiles" ("PackageId");
CREATE INDEX IF NOT EXISTS "IX_package_code_page_profiles_UpdatedByUserId" ON "package_code_page_profiles" ("UpdatedByUserId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260408190000_AddPackageCodePageProfiles', '9.0.3'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260408190000_AddPackageCodePageProfiles');

COMMIT;

-- ============================================================
-- Migration 5: 20260409141216_AddCommunityCommentModerationAndCriticalExamFixes
-- ============================================================
BEGIN;

ALTER TABLE "student_answers" ADD COLUMN IF NOT EXISTS "SubmittedText" character varying(2000);
ALTER TABLE "community_post_comments" ADD COLUMN IF NOT EXISTS "RejectionReason" character varying(1000);
ALTER TABLE "community_post_comments" ADD COLUMN IF NOT EXISTS "ReviewedAt" timestamp without time zone;
ALTER TABLE "community_post_comments" ADD COLUMN IF NOT EXISTS "ReviewedByUserId" uuid;
ALTER TABLE "community_post_comments" ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS "IX_community_post_comments_ReviewedByUserId" ON "community_post_comments" ("ReviewedByUserId");
CREATE INDEX IF NOT EXISTS "IX_community_post_comments_Status" ON "community_post_comments" ("Status");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_community_post_comments_users_ReviewedByUserId'
    ) THEN
        ALTER TABLE "community_post_comments"
            ADD CONSTRAINT "FK_community_post_comments_users_ReviewedByUserId"
            FOREIGN KEY ("ReviewedByUserId") REFERENCES "users" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260409141216_AddCommunityCommentModerationAndCriticalExamFixes', '9.0.3'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260409141216_AddCommunityCommentModerationAndCriticalExamFixes');

COMMIT;

-- ============================================================
-- Migration 6: 20260409173000_AddPhase2DataIntegrityFixes
-- Creates essay_submissions and ExtraWatchRequests columns only if tables exist
-- ============================================================
BEGIN;

-- essay_submissions may not exist yet - create column only if table exists
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'essay_submissions') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'essay_submissions' AND column_name = 'AudioUrl') THEN
            ALTER TABLE "essay_submissions" ADD COLUMN "AudioUrl" character varying(2000);
        END IF;
    END IF;
END $$;

-- ExtraWatchRequests may not exist yet - create column only if table exists
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ExtraWatchRequests') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ExtraWatchRequests' AND column_name = 'RejectionReason') THEN
            ALTER TABLE "ExtraWatchRequests" ADD COLUMN "RejectionReason" character varying(1000);
        END IF;
    END IF;
END $$;

ALTER TABLE "student_profiles" ADD COLUMN IF NOT EXISTS "CurrentMode" character varying(10) NOT NULL DEFAULT 'light';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260409173000_AddPhase2DataIntegrityFixes', '9.0.3'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260409173000_AddPhase2DataIntegrityFixes');

COMMIT;

SELECT 'All 6 migrations applied successfully!' AS result;
