using System;
using Microsoft.EntityFrameworkCore.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260613173000_FixMissingTables")]
    public partial class FixMissingTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create ExtraWatchRequests table and indexes
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ExtraWatchRequests"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedAt"" timestamp without time zone NOT NULL,
                    ""LessonVideoId"" uuid NOT NULL,
                    ""RejectionReason"" character varying(1000) NULL,
                    ""ResolvedAt"" timestamp without time zone NULL,
                    ""Status"" integer NOT NULL,
                    ""UpdatedAt"" timestamp without time zone NULL,
                    ""UserId"" uuid NOT NULL,
                    CONSTRAINT ""PK_ExtraWatchRequests"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_ExtraWatchRequests_lesson_videos_LessonVideoId"" FOREIGN KEY (""LessonVideoId"") REFERENCES lesson_videos (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_ExtraWatchRequests_users_UserId"" FOREIGN KEY (""UserId"") REFERENCES users (""Id"") ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ""IX_ExtraWatchRequests_LessonVideoId"" ON ""ExtraWatchRequests"" (""LessonVideoId"");
                CREATE INDEX IF NOT EXISTS ""IX_ExtraWatchRequests_UserId"" ON ""ExtraWatchRequests"" (""UserId"");
            ");

            // Create package_code_page_profiles table and indexes
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS package_code_page_profiles (
                    ""Id"" uuid NOT NULL,
                    ""PackageId"" uuid NOT NULL,
                    ""Status"" integer NOT NULL,
                    ""HeroEyebrow"" character varying(80) NULL,
                    ""HeroTitle"" character varying(140) NULL,
                    ""HeroDescription"" character varying(600) NULL,
                    ""OfferTitle"" character varying(120) NULL,
                    ""OfferDescription"" character varying(600) NULL,
                    ""ActivationTitle"" character varying(120) NULL,
                    ""ActivationDescription"" character varying(500) NULL,
                    ""SupportTitle"" character varying(120) NULL,
                    ""SupportDescription"" character varying(400) NULL,
                    ""ThemeAccentKey"" character varying(60) NULL,
                    ""UpdatedByUserId"" uuid NULL,
                    ""PublishedAt"" timestamp without time zone NULL,
                    ""CreatedAt"" timestamp without time zone NOT NULL,
                    ""UpdatedAt"" timestamp without time zone NULL,
                    CONSTRAINT ""PK_package_code_page_profiles"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_package_code_page_profiles_packages_PackageId"" FOREIGN KEY (""PackageId"") REFERENCES packages (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_package_code_page_profiles_users_UpdatedByUserId"" FOREIGN KEY (""UpdatedByUserId"") REFERENCES users (""Id"") ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_package_code_page_profiles_PackageId"" ON package_code_page_profiles (""PackageId"");
                CREATE INDEX IF NOT EXISTS ""IX_package_code_page_profiles_UpdatedByUserId"" ON package_code_page_profiles (""UpdatedByUserId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExtraWatchRequests");
            migrationBuilder.DropTable(name: "package_code_page_profiles");
        }
    }
}
