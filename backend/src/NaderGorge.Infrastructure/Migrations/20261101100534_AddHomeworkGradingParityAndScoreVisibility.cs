using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeworkGradingParityAndScoreVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EssayGradingMode",
                table: "homeworks",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAnswersToStudents",
                table: "homeworks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowScoreToStudents",
                table: "homeworks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SingleAttemptOnly",
                table: "homeworks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GradingFeedback",
                table: "homework_answers",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreciseScoreReceived",
                table: "homework_answers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowScoreToStudents",
                table: "exams",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Earlier callbacks retained explanations only in the audit trail.
            // Never restore an explanation superseded by a manual grade or revision.
            migrationBuilder.Sql("""
                WITH callbacks AS MATERIALIZED (
                    SELECT "EntityId", "CreatedAt", "Id", "NewValues"::jsonb AS grades
                    FROM audit_logs
                    WHERE "Action" = 'HomeworkAIGraded' AND "NewValues" IS NOT NULL
                ), explanations AS (
                    SELECT DISTINCT ON (callback."EntityId", grade->>'AnswerId')
                        callback."EntityId" AS submission_id, callback."CreatedAt" AS graded_at,
                        grade->>'AnswerId' AS answer_id, grade->>'Feedback' AS feedback,
                        (grade->>'Score')::numeric AS score
                    FROM callbacks callback
                    CROSS JOIN LATERAL jsonb_array_elements(callback.grades) grade
                    ORDER BY callback."EntityId", grade->>'AnswerId', callback."CreatedAt" DESC, callback."Id" DESC
                )
                UPDATE homework_answers answer
                SET "GradingFeedback" = left(explanation.feedback, 5000)
                FROM explanations explanation, homework_submissions submission
                WHERE answer."Id"::text = explanation.answer_id
                  AND answer."HomeworkSubmissionId" = explanation.submission_id
                  AND submission."Id" = explanation.submission_id
                  AND submission."AssistantReviewerId" IS NULL
                  AND answer."GradingFeedback" IS NULL
                  AND length(trim(explanation.feedback)) > 0
                  AND ((explanation.score = 0 AND answer."ScoreReceived" = 0)
                    OR (explanation.score = 1 AND answer."ScoreReceived" > 0))
                  AND NOT EXISTS (
                    SELECT 1 FROM audit_logs later
                    WHERE later."EntityId" = submission."Id"
                      AND later."CreatedAt" >= explanation.graded_at
                      AND later."Action" IN ('AssessmentManuallyGraded', 'AssessmentAttemptRegraded', 'HomeworkRevisionCompleted')
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EssayGradingMode",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "ShowAnswersToStudents",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "ShowScoreToStudents",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "SingleAttemptOnly",
                table: "homeworks");

            migrationBuilder.DropColumn(
                name: "GradingFeedback",
                table: "homework_answers");

            migrationBuilder.DropColumn(
                name: "PreciseScoreReceived",
                table: "homework_answers");

            migrationBuilder.DropColumn(
                name: "ShowScoreToStudents",
                table: "exams");
        }
    }
}
