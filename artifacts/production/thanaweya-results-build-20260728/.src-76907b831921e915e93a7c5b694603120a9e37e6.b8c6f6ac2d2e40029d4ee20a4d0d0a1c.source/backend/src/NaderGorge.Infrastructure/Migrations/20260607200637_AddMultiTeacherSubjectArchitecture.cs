using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTeacherSubjectArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0. Create essay_submissions table (missing from previous migrations but in model snapshot)
            migrationBuilder.CreateTable(
                name: "essay_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentExamAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerText = table.Column<string>(type: "text", nullable: false),
                    AudioUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AiInitialScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AiFeedback = table.Column<string>(type: "text", nullable: true),
                    TeacherFinalScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TeacherFeedback = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_essay_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_essay_submissions_question_bank_items_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "question_bank_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_essay_submissions_student_exam_attempts_StudentExamAttemptId",
                        column: x => x.StudentExamAttemptId,
                        principalTable: "student_exam_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_essay_submissions_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_essay_submissions_QuestionId",
                table: "essay_submissions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_essay_submissions_StudentExamAttemptId",
                table: "essay_submissions",
                column: "StudentExamAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_essay_submissions_StudentId",
                table: "essay_submissions",
                column: "StudentId");

            // 1. Create subjects table
            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subjects", x => x.Id);
                });

            // 2. Create teacher_profiles table
            migrationBuilder.CreateTable(
                name: "teacher_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bio = table.Column<string>(type: "text", nullable: false),
                    Specialization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContactInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 3. Create teacher_subjects table
            migrationBuilder.CreateTable(
                name: "teacher_subjects",
                columns: table => new
                {
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_subjects", x => new { x.TeacherId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_teacher_subjects_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teacher_subjects_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 4. Seed default subject, teacher user, teacher profile, and teacher-subject connection
            migrationBuilder.Sql(@"
                INSERT INTO users (""Id"", ""FullName"", ""PhoneNumber"", ""PasswordHash"", ""IsActive"", ""IsProfileComplete"", ""CreatedAt"")
                VALUES ('c4b82937-293e-48a3-a002-decf9a1efab8', 'مدرس تاريخ افتراضي', '01111111111', '$2a$11$wK1mJz3B.gZq6u.RjT1RquWvV0G9t0h6YI4tZpXg9gq/o0s0z0z0', true, true, NOW())
                ON CONFLICT (""PhoneNumber"") DO NOTHING;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO user_roles (""UserId"", ""RoleId"")
                SELECT 'c4b82937-293e-48a3-a002-decf9a1efab8', ""Id""
                FROM roles WHERE ""Name"" = 'Teacher'
                ON CONFLICT DO NOTHING;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO subjects (""Id"", ""Name"", ""NormalizedName"", ""Description"", ""CreatedAt"")
                VALUES ('d9b8a342-990a-4286-905e-fdebb2e3895e', 'التاريخ', 'history', 'مادة التاريخ للثانوية العامة', NOW())
                ON CONFLICT DO NOTHING;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO teacher_profiles (""Id"", ""UserId"", ""Bio"", ""Specialization"", ""CommissionRate"", ""ContactInfo"", ""CreatedAt"")
                VALUES ('b4b82937-293e-48a3-a002-decf9a1efab8', 'c4b82937-293e-48a3-a002-decf9a1efab8', 'المدرس الافتراضي للمنصة', 'التاريخ', 10.00, '01111111111', NOW())
                ON CONFLICT DO NOTHING;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO teacher_subjects (""TeacherId"", ""SubjectId"")
                VALUES ('b4b82937-293e-48a3-a002-decf9a1efab8', 'd9b8a342-990a-4286-905e-fdebb2e3895e')
                ON CONFLICT DO NOTHING;
            ");

            // 5. Add columns with default values pointing to seeded entities
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByTeacherId",
                table: "question_bank_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b4b82937-293e-48a3-a002-decf9a1efab8"));

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "question_bank_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("d9b8a342-990a-4286-905e-fdebb2e3895e"));

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "programs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("d9b8a342-990a-4286-905e-fdebb2e3895e"));

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "packages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b4b82937-293e-48a3-a002-decf9a1efab8"));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByTeacherId",
                table: "exams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b4b82937-293e-48a3-a002-decf9a1efab8"));

            migrationBuilder.AddColumn<Guid>(
                name: "GradedByTeacherId",
                table: "essay_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "code_groups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b4b82937-293e-48a3-a002-decf9a1efab8"));

            // 6. Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_question_bank_items_CreatedByTeacherId",
                table: "question_bank_items",
                column: "CreatedByTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_question_bank_items_SubjectId",
                table: "question_bank_items",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_SubjectId",
                table: "programs",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_packages_TeacherId",
                table: "packages",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_exams_CreatedByTeacherId",
                table: "exams",
                column: "CreatedByTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_essay_submissions_GradedByTeacherId",
                table: "essay_submissions",
                column: "GradedByTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_code_groups_TeacherId",
                table: "code_groups",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_subjects_NormalizedName",
                table: "subjects",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_UserId",
                table: "teacher_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_subjects_SubjectId",
                table: "teacher_subjects",
                column: "SubjectId");

            // 7. Add foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_code_groups_teacher_profiles_TeacherId",
                table: "code_groups",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_essay_submissions_teacher_profiles_GradedByTeacherId",
                table: "essay_submissions",
                column: "GradedByTeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_exams_teacher_profiles_CreatedByTeacherId",
                table: "exams",
                column: "CreatedByTeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_packages_teacher_profiles_TeacherId",
                table: "packages",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_programs_subjects_SubjectId",
                table: "programs",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_question_bank_items_subjects_SubjectId",
                table: "question_bank_items",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_question_bank_items_teacher_profiles_CreatedByTeacherId",
                table: "question_bank_items",
                column: "CreatedByTeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_code_groups_teacher_profiles_TeacherId",
                table: "code_groups");

            migrationBuilder.DropForeignKey(
                name: "FK_essay_submissions_teacher_profiles_GradedByTeacherId",
                table: "essay_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_exams_teacher_profiles_CreatedByTeacherId",
                table: "exams");

            migrationBuilder.DropForeignKey(
                name: "FK_packages_teacher_profiles_TeacherId",
                table: "packages");

            migrationBuilder.DropForeignKey(
                name: "FK_programs_subjects_SubjectId",
                table: "programs");

            migrationBuilder.DropForeignKey(
                name: "FK_question_bank_items_subjects_SubjectId",
                table: "question_bank_items");

            migrationBuilder.DropForeignKey(
                name: "FK_question_bank_items_teacher_profiles_CreatedByTeacherId",
                table: "question_bank_items");

            migrationBuilder.DropTable(
                name: "teacher_subjects");

            migrationBuilder.DropTable(
                name: "subjects");

            migrationBuilder.DropTable(
                name: "teacher_profiles");

            migrationBuilder.DropTable(
                name: "essay_submissions");

            migrationBuilder.DropIndex(
                name: "IX_question_bank_items_CreatedByTeacherId",
                table: "question_bank_items");

            migrationBuilder.DropIndex(
                name: "IX_question_bank_items_SubjectId",
                table: "question_bank_items");

            migrationBuilder.DropIndex(
                name: "IX_programs_SubjectId",
                table: "programs");

            migrationBuilder.DropIndex(
                name: "IX_packages_TeacherId",
                table: "packages");

            migrationBuilder.DropIndex(
                name: "IX_exams_CreatedByTeacherId",
                table: "exams");

            migrationBuilder.DropIndex(
                name: "IX_essay_submissions_GradedByTeacherId",
                table: "essay_submissions");

            migrationBuilder.DropIndex(
                name: "IX_code_groups_TeacherId",
                table: "code_groups");

            migrationBuilder.DropColumn(
                name: "CreatedByTeacherId",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "CreatedByTeacherId",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "GradedByTeacherId",
                table: "essay_submissions");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "code_groups");
        }
    }
}
