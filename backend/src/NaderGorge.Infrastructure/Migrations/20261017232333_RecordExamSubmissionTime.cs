using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecordExamSubmissionTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "student_exam_attempts",
                type: "timestamp without time zone",
                nullable: true);

            // Last modification may reflect grading. Match both attempt and exam to prove hand-in time.
            migrationBuilder.Sql("""
                WITH submission_events AS (
                    SELECT "CreatedAt",
                        CASE WHEN "PayloadJson" IS JSON OBJECT
                            THEN "PayloadJson"::jsonb ELSE '{}'::jsonb END AS payload
                    FROM outbox_events
                    WHERE "Type" = 'ExamSubmitted'
                ), submitted_attempts AS (
                    SELECT attempt."Id", MIN(event."CreatedAt") AS submitted_at
                    FROM student_exam_attempts AS attempt
                    JOIN submission_events AS event
                        ON event.payload->>'attemptId' = attempt."Id"::text
                        AND event.payload->>'examId' = attempt."ExamId"::text
                    GROUP BY attempt."Id"
                )
                UPDATE student_exam_attempts AS attempt
                SET "SubmittedAt" = submitted_attempts.submitted_at
                FROM submitted_attempts
                WHERE attempt."Id" = submitted_attempts."Id"
                    AND attempt."SubmittedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "student_exam_attempts");
        }
    }
}
