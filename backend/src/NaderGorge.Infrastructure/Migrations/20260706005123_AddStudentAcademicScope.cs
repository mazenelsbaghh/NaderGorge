using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAcademicScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "academic_subject_eligibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationStage = table.Column<int>(type: "integer", nullable: false),
                    GradeLevel = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_subject_eligibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_academic_subject_eligibilities_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_facing_academic_scopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeLevel = table.Column<int>(type: "integer", nullable: false),
                    EducationStage = table.Column<int>(type: "integer", nullable: true),
                    GradeLevel = table.Column<int>(type: "integer", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_facing_academic_scopes", x => x.Id);
                    table.CheckConstraint("CK_student_facing_scopes_shape", "(\"ScopeLevel\" = 1 AND \"EducationStage\" IS NULL AND \"GradeLevel\" IS NULL AND \"SubjectId\" IS NULL) OR (\"ScopeLevel\" = 2 AND \"EducationStage\" IS NOT NULL AND \"GradeLevel\" IS NULL AND \"SubjectId\" IS NULL) OR (\"ScopeLevel\" = 3 AND \"EducationStage\" IS NOT NULL AND \"GradeLevel\" IS NOT NULL AND \"SubjectId\" IS NULL) OR (\"ScopeLevel\" = 0 AND \"EducationStage\" IS NOT NULL AND \"GradeLevel\" IS NOT NULL AND \"SubjectId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_student_facing_academic_scopes_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_facing_academic_scopes_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_subject_eligibilities_EducationStage_GradeLevel_Su~",
                table: "academic_subject_eligibilities",
                columns: new[] { "EducationStage", "GradeLevel", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_subject_eligibilities_EducationStage_GradeLevel_Is~",
                table: "academic_subject_eligibilities",
                columns: new[] { "EducationStage", "GradeLevel", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_subject_eligibilities_SubjectId_IsActive_Education~",
                table: "academic_subject_eligibilities",
                columns: new[] { "SubjectId", "IsActive", "EducationStage", "GradeLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_subject_eligibilities_SubjectId",
                table: "academic_subject_eligibilities",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_student_facing_academic_scopes_CreatedByUserId",
                table: "student_facing_academic_scopes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_student_facing_academic_scopes_OwnerType_OwnerId",
                table: "student_facing_academic_scopes",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_facing_academic_scopes_OwnerType_OwnerId_ScopeLevel~",
                table: "student_facing_academic_scopes",
                columns: new[] { "OwnerType", "OwnerId", "ScopeLevel", "EducationStage", "GradeLevel", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_facing_academic_scopes_ScopeLevel_EducationStage_Gr~",
                table: "student_facing_academic_scopes",
                columns: new[] { "ScopeLevel", "EducationStage", "GradeLevel", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_facing_academic_scopes_SubjectId",
                table: "student_facing_academic_scopes",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_EducationStage_GradeLevel_UserId",
                table: "student_profiles",
                columns: new[] { "EducationStage", "GradeLevel", "UserId" });

            migrationBuilder.Sql("""
                WITH grade_alias(alias_value, education_stage, grade_level) AS (
                    VALUES
                        ('firstsecondary', 0, 0),
                        ('1st secondary', 0, 0),
                        ('الأول الثانوي', 0, 0),
                        ('الاول الثانوي', 0, 0),
                        ('الأول الثانوى', 0, 0),
                        ('اولى ثانوي', 0, 0),
                        ('secondsecondary', 0, 1),
                        ('2nd secondary', 0, 1),
                        ('الثاني الثانوي', 0, 1),
                        ('الثانى الثانوي', 0, 1),
                        ('الثاني الثانوى', 0, 1),
                        ('تانية ثانوي', 0, 1),
                        ('secondarygrade3', 0, 31),
                        ('thirdsecondary', 0, 31),
                        ('3rd secondary', 0, 31),
                        ('الثالث الثانوي', 0, 31),
                        ('الثالث الثانوى', 0, 31),
                        ('ثالثة ثانوي', 0, 31),
                        ('firstbaccalaureate', 1, 2),
                        ('secondbaccalaureate', 1, 3),
                        ('primarygrade1', 2, 10),
                        ('primarygrade2', 2, 11),
                        ('primarygrade3', 2, 12),
                        ('primarygrade4', 2, 13),
                        ('primarygrade5', 2, 14),
                        ('primarygrade6', 2, 15),
                        ('prepgrade1', 3, 20),
                        ('prepgrade2', 3, 21),
                        ('prepgrade3', 3, 22),
                        ('azhariprimary1', 4, 40),
                        ('azhariprimary2', 4, 41),
                        ('azhariprimary3', 4, 42),
                        ('azhariprimary4', 4, 43),
                        ('azhariprimary5', 4, 44),
                        ('azhariprimary6', 4, 45),
                        ('azhariprep1', 4, 50),
                        ('azhariprep2', 4, 51),
                        ('azhariprep3', 4, 52),
                        ('azharisecondary1', 4, 60),
                        ('azharisecondary2', 4, 61),
                        ('azharisecondary3', 4, 62),
                        ('americangrade1', 5, 70),
                        ('americangrade2', 5, 71),
                        ('americangrade3', 5, 72),
                        ('americangrade4', 5, 73),
                        ('americangrade5', 5, 74),
                        ('americangrade6', 5, 75),
                        ('americangrade7', 5, 76),
                        ('americangrade8', 5, 77),
                        ('americangrade9', 5, 78),
                        ('americangrade10', 5, 79),
                        ('americangrade11', 5, 80),
                        ('americangrade12', 5, 81)
                ),
                inferred_eligibilities AS (
                    SELECT DISTINCT ga.education_stage, ga.grade_level, p."SubjectId"
                    FROM packages p
                    JOIN grade_alias ga ON lower(trim(p."TargetGrade")) = ga.alias_value
                    WHERE p."SubjectId" IS NOT NULL

                    UNION

                    SELECT DISTINCT ga.education_stage, ga.grade_level, pep."SubjectId"
                    FROM public_exam_products pep
                    JOIN grade_alias ga ON lower(trim(coalesce(pep."GradeLevel", ''))) = ga.alias_value
                    WHERE pep."SubjectId" IS NOT NULL

                    UNION

                    SELECT DISTINCT stp."EducationStage", stp."GradeLevel", stpt."SubjectId"
                    FROM shared_teacher_packages stp
                    JOIN shared_teacher_package_teachers stpt ON stpt."SharedTeacherPackageId" = stp."Id"
                    WHERE stp."EducationStage" IS NOT NULL
                      AND stp."GradeLevel" IS NOT NULL
                      AND stpt."SubjectId" IS NOT NULL

                    UNION

                    SELECT DISTINCT ga.education_stage, ga.grade_level, ts."SubjectId"
                    FROM teacher_subjects ts
                    JOIN packages p ON p."TeacherId" = ts."TeacherId" AND p."SubjectId" = ts."SubjectId"
                    JOIN grade_alias ga ON lower(trim(p."TargetGrade")) = ga.alias_value
                )
                INSERT INTO academic_subject_eligibilities ("Id", "EducationStage", "GradeLevel", "SubjectId", "IsActive", "CreatedAt")
                SELECT
                    (substr(md5('academic-subject-eligibility:' || education_stage || ':' || grade_level || ':' || "SubjectId"), 1, 8)
                        || '-' || substr(md5('academic-subject-eligibility:' || education_stage || ':' || grade_level || ':' || "SubjectId"), 9, 4)
                        || '-' || substr(md5('academic-subject-eligibility:' || education_stage || ':' || grade_level || ':' || "SubjectId"), 13, 4)
                        || '-' || substr(md5('academic-subject-eligibility:' || education_stage || ':' || grade_level || ':' || "SubjectId"), 17, 4)
                        || '-' || substr(md5('academic-subject-eligibility:' || education_stage || ':' || grade_level || ':' || "SubjectId"), 21, 12))::uuid,
                    education_stage,
                    grade_level,
                    "SubjectId",
                    TRUE,
                    NOW()::timestamp
                FROM inferred_eligibilities
                ON CONFLICT ("EducationStage", "GradeLevel", "SubjectId") DO NOTHING;
                """);

            migrationBuilder.Sql("""
                WITH grade_alias(alias_value, education_stage, grade_level) AS (
                    VALUES
                        ('firstsecondary', 0, 0),
                        ('1st secondary', 0, 0),
                        ('الأول الثانوي', 0, 0),
                        ('الاول الثانوي', 0, 0),
                        ('الأول الثانوى', 0, 0),
                        ('اولى ثانوي', 0, 0),
                        ('secondsecondary', 0, 1),
                        ('2nd secondary', 0, 1),
                        ('الثاني الثانوي', 0, 1),
                        ('الثانى الثانوي', 0, 1),
                        ('الثاني الثانوى', 0, 1),
                        ('تانية ثانوي', 0, 1),
                        ('secondarygrade3', 0, 31),
                        ('thirdsecondary', 0, 31),
                        ('3rd secondary', 0, 31),
                        ('الثالث الثانوي', 0, 31),
                        ('الثالث الثانوى', 0, 31),
                        ('ثالثة ثانوي', 0, 31),
                        ('firstbaccalaureate', 1, 2),
                        ('secondbaccalaureate', 1, 3),
                        ('primarygrade1', 2, 10),
                        ('primarygrade2', 2, 11),
                        ('primarygrade3', 2, 12),
                        ('primarygrade4', 2, 13),
                        ('primarygrade5', 2, 14),
                        ('primarygrade6', 2, 15),
                        ('prepgrade1', 3, 20),
                        ('prepgrade2', 3, 21),
                        ('prepgrade3', 3, 22),
                        ('azhariprimary1', 4, 40),
                        ('azhariprimary2', 4, 41),
                        ('azhariprimary3', 4, 42),
                        ('azhariprimary4', 4, 43),
                        ('azhariprimary5', 4, 44),
                        ('azhariprimary6', 4, 45),
                        ('azhariprep1', 4, 50),
                        ('azhariprep2', 4, 51),
                        ('azhariprep3', 4, 52),
                        ('azharisecondary1', 4, 60),
                        ('azharisecondary2', 4, 61),
                        ('azharisecondary3', 4, 62),
                        ('americangrade1', 5, 70),
                        ('americangrade2', 5, 71),
                        ('americangrade3', 5, 72),
                        ('americangrade4', 5, 73),
                        ('americangrade5', 5, 74),
                        ('americangrade6', 5, 75),
                        ('americangrade7', 5, 76),
                        ('americangrade8', 5, 77),
                        ('americangrade9', 5, 78),
                        ('americangrade10', 5, 79),
                        ('americangrade11', 5, 80),
                        ('americangrade12', 5, 81)
                ),
                inferred_scopes AS (
                    SELECT 0 AS owner_type, p."Id" AS owner_id, 1 AS scope_level, NULL::integer AS education_stage, NULL::integer AS grade_level, NULL::uuid AS subject_id
                    FROM packages p
                    WHERE lower(trim(p."TargetGrade")) IN ('all', 'جميع الصفوف الدراسية', 'كل الصفوف')

                    UNION ALL

                    SELECT 0 AS owner_type, p."Id" AS owner_id, 0 AS scope_level, ga.education_stage, ga.grade_level, p."SubjectId" AS subject_id
                    FROM packages p
                    JOIN grade_alias ga ON lower(trim(p."TargetGrade")) = ga.alias_value
                    WHERE p."SubjectId" IS NOT NULL

                    UNION ALL

                    SELECT 6 AS owner_type, pep."Id" AS owner_id, 1 AS scope_level, NULL::integer AS education_stage, NULL::integer AS grade_level, NULL::uuid AS subject_id
                    FROM public_exam_products pep
                    WHERE pep."IsPlatformWide" = TRUE

                    UNION ALL

                    SELECT
                        6 AS owner_type,
                        pep."Id" AS owner_id,
                        CASE WHEN pep."SubjectId" IS NULL THEN 3 ELSE 0 END AS scope_level,
                        ga.education_stage,
                        ga.grade_level,
                        pep."SubjectId" AS subject_id
                    FROM public_exam_products pep
                    JOIN grade_alias ga ON lower(trim(coalesce(pep."GradeLevel", ''))) = ga.alias_value
                    WHERE pep."IsPlatformWide" = FALSE

                    UNION ALL

                    SELECT 14 AS owner_type, stp."Id" AS owner_id, 3 AS scope_level, stp."EducationStage" AS education_stage, stp."GradeLevel" AS grade_level, NULL::uuid AS subject_id
                    FROM shared_teacher_packages stp
                    WHERE stp."EducationStage" IS NOT NULL AND stp."GradeLevel" IS NOT NULL

                    UNION ALL

                    SELECT 7 AS owner_type, ts."TeacherId" AS owner_id, 0 AS scope_level, ase."EducationStage" AS education_stage, ase."GradeLevel" AS grade_level, ts."SubjectId" AS subject_id
                    FROM teacher_subjects ts
                    JOIN academic_subject_eligibilities ase ON ase."SubjectId" = ts."SubjectId" AND ase."IsActive" = TRUE
                )
                INSERT INTO student_facing_academic_scopes ("Id", "OwnerType", "OwnerId", "ScopeLevel", "EducationStage", "GradeLevel", "SubjectId", "CreatedAt")
                SELECT
                    (substr(md5('student-facing-scope:' || owner_type || ':' || owner_id || ':' || scope_level || ':' || coalesce(education_stage::text, '') || ':' || coalesce(grade_level::text, '') || ':' || coalesce(subject_id::text, '')), 1, 8)
                        || '-' || substr(md5('student-facing-scope:' || owner_type || ':' || owner_id || ':' || scope_level || ':' || coalesce(education_stage::text, '') || ':' || coalesce(grade_level::text, '') || ':' || coalesce(subject_id::text, '')), 9, 4)
                        || '-' || substr(md5('student-facing-scope:' || owner_type || ':' || owner_id || ':' || scope_level || ':' || coalesce(education_stage::text, '') || ':' || coalesce(grade_level::text, '') || ':' || coalesce(subject_id::text, '')), 13, 4)
                        || '-' || substr(md5('student-facing-scope:' || owner_type || ':' || owner_id || ':' || scope_level || ':' || coalesce(education_stage::text, '') || ':' || coalesce(grade_level::text, '') || ':' || coalesce(subject_id::text, '')), 17, 4)
                        || '-' || substr(md5('student-facing-scope:' || owner_type || ':' || owner_id || ':' || scope_level || ':' || coalesce(education_stage::text, '') || ':' || coalesce(grade_level::text, '') || ':' || coalesce(subject_id::text, '')), 21, 12))::uuid,
                    owner_type,
                    owner_id,
                    scope_level,
                    education_stage,
                    grade_level,
                    subject_id,
                    NOW()::timestamp
                FROM inferred_scopes inferred
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM student_facing_academic_scopes existing
                    WHERE existing."OwnerType" = inferred.owner_type
                      AND existing."OwnerId" = inferred.owner_id
                      AND existing."ScopeLevel" = inferred.scope_level
                      AND existing."EducationStage" IS NOT DISTINCT FROM inferred.education_stage
                      AND existing."GradeLevel" IS NOT DISTINCT FROM inferred.grade_level
                      AND existing."SubjectId" IS NOT DISTINCT FROM inferred.subject_id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_student_profiles_EducationStage_GradeLevel_UserId",
                table: "student_profiles");

            migrationBuilder.DropTable(
                name: "academic_subject_eligibilities");

            migrationBuilder.DropTable(
                name: "student_facing_academic_scopes");
        }
    }
}
