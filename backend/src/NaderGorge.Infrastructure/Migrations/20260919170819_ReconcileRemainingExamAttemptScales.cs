using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileRemainingExamAttemptScales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '10s';
                LOCK TABLE student_exam_attempts, student_answers IN SHARE ROW EXCLUSIVE MODE;

                WITH candidates AS (
                    SELECT a."Id",
                        a."DefinitionSnapshotJson" AS snapshot,
                        coalesce((a."DefinitionSnapshotJson"->>'TotalScore')::numeric, 0) AS old_total,
                        coalesce((a."DefinitionSnapshotJson"->>'PassingScore')::numeric, 0) AS old_passing,
                        coalesce((a."DefinitionSnapshotJson"#>>'{Revision,MinimumScoreRatio}')::numeric, 0) AS minimum_ratio,
                        (
                            SELECT coalesce(sum((question->>'Points')::numeric), 0)
                            FROM jsonb_array_elements(a."DefinitionSnapshotJson"->'Questions') AS question
                        ) AS assigned_total
                    FROM student_exam_attempts a
                    WHERE a."DefinitionSnapshotJson" IS NOT NULL
                        AND a."DefinitionSnapshotJson"->>'Kind' = 'exam'
                        AND jsonb_typeof(a."DefinitionSnapshotJson"->'Questions') = 'array'
                        AND jsonb_typeof(a."DefinitionSnapshotJson"->'ReserveQuestions') = 'array'
                        AND jsonb_array_length(a."DefinitionSnapshotJson"->'ReserveQuestions') > 0
                        AND NOT coalesce((a."DefinitionSnapshotJson"->>'UsesAssignedQuestionPoints')::boolean, false)
                ), corrected AS (
                    SELECT c."Id", c.snapshot, c.old_total, c.old_passing, c.assigned_total,
                        greatest(
                            coalesce(sum(answer."PointsAwarded"), 0),
                            round(c.minimum_ratio * c.assigned_total, 2)
                        ) AS corrected_score
                    FROM candidates c
                    LEFT JOIN student_answers answer ON answer."StudentExamAttemptId" = c."Id"
                    WHERE c.assigned_total > 0
                    GROUP BY c."Id", c.snapshot, c.old_total, c.old_passing, c.assigned_total, c.minimum_ratio
                )
                UPDATE student_exam_attempts attempt
                SET "DefinitionSnapshotJson" = jsonb_set(
                        jsonb_set(
                            jsonb_set(corrected.snapshot, '{TotalScore}', to_jsonb(corrected.assigned_total), true),
                            '{PassingScore}',
                            to_jsonb(CASE WHEN corrected.old_total > 0
                                THEN round(corrected.old_passing / corrected.old_total * corrected.assigned_total, 2)
                                ELSE 0 END),
                            true),
                        '{UsesAssignedQuestionPoints}', 'true'::jsonb, true),
                    "ScoreAchieved" = CASE WHEN attempt."IsTimeExpired" THEN 0 ELSE corrected.corrected_score END,
                    "UpdatedAt" = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
                FROM corrected
                WHERE attempt."Id" = corrected."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Attempt score corrections preserve authoritative grades and require a reviewed forward migration.");
        }
    }
}
