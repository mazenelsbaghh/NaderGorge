using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2AcademicOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assistant_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAssistantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assistant_tasks_users_AssignedAssistantId",
                        column: x => x.AssignedAssistantId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_assistant_tasks_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gamification_action_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gamification_action_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gamification_action_logs_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "homeworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    PassingScoreThreshold = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_homeworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_events_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_badges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeName = table.Column<string>(type: "text", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_badges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_badges_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_gamifications",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    CurrentStreakCount = table.Column<int>(type: "integer", nullable: false),
                    LongestStreakCount = table.Column<int>(type: "integer", nullable: false),
                    LastTaskCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LevelName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_gamifications", x => x.StudentId);
                    table.ForeignKey(
                        name: "FK_student_gamifications_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_status_trackers",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    ConsecutiveMissedHomeworks = table.Column<int>(type: "integer", nullable: false),
                    ConsecutiveFailedExams = table.Column<int>(type: "integer", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastEvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_status_trackers", x => x.StudentId);
                    table.ForeignKey(
                        name: "FK_student_status_trackers_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warning_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    TriggerReason = table.Column<string>(type: "text", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedByAssistantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warning_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warning_events_users_ResolvedByAssistantId",
                        column: x => x.ResolvedByAssistantId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warning_events_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "homework_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    QuestionType = table.Column<int>(type: "integer", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    PossibleAnswers = table.Column<string[]>(type: "text[]", nullable: true),
                    CorrectAnswerKey = table.Column<string>(type: "text", nullable: true),
                    PointsActive = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_homework_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_homework_questions_homeworks_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "homeworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "homework_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GradedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssistantReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssistantNotes = table.Column<string>(type: "text", nullable: true),
                    OverallScore = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_homework_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_homework_submissions_homeworks_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "homeworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_homework_submissions_users_AssistantReviewerId",
                        column: x => x.AssistantReviewerId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_homework_submissions_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "homework_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeworkSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvidedAnswer = table.Column<string>(type: "text", nullable: false),
                    ScoreReceived = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_homework_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_homework_answers_homework_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "homework_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_homework_answers_homework_submissions_HomeworkSubmissionId",
                        column: x => x.HomeworkSubmissionId,
                        principalTable: "homework_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assistant_tasks_AssignedAssistantId",
                table: "assistant_tasks",
                column: "AssignedAssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_assistant_tasks_StudentId",
                table: "assistant_tasks",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_gamification_action_logs_StudentId",
                table: "gamification_action_logs",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_homework_answers_HomeworkSubmissionId",
                table: "homework_answers",
                column: "HomeworkSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_homework_answers_QuestionId",
                table: "homework_answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_homework_questions_HomeworkId",
                table: "homework_questions",
                column: "HomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_homework_submissions_AssistantReviewerId",
                table: "homework_submissions",
                column: "AssistantReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_homework_submissions_HomeworkId",
                table: "homework_submissions",
                column: "HomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_homework_submissions_StudentId",
                table: "homework_submissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_events_UserId",
                table: "notification_events",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_student_badges_StudentId",
                table: "student_badges",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_warning_events_ResolvedByAssistantId",
                table: "warning_events",
                column: "ResolvedByAssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_warning_events_StudentId",
                table: "warning_events",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_tasks");

            migrationBuilder.DropTable(
                name: "gamification_action_logs");

            migrationBuilder.DropTable(
                name: "homework_answers");

            migrationBuilder.DropTable(
                name: "notification_events");

            migrationBuilder.DropTable(
                name: "student_badges");

            migrationBuilder.DropTable(
                name: "student_gamifications");

            migrationBuilder.DropTable(
                name: "student_status_trackers");

            migrationBuilder.DropTable(
                name: "warning_events");

            migrationBuilder.DropTable(
                name: "homework_questions");

            migrationBuilder.DropTable(
                name: "homework_submissions");

            migrationBuilder.DropTable(
                name: "homeworks");
        }
    }
}
