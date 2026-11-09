using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Queries;

internal static class TransferredCourseExamResults
{
    // Only the destination exams in Nader's second-month section use the transfer policy.
    internal static readonly Guid LectureNineExamId = new("bd0b0b8f-5981-4a38-b514-efb1bcdb2d9d");
    private static readonly Guid LectureTenExamId = new("6e6f1bba-559d-428a-9666-6a31006fe763");
    private static readonly Guid LectureElevenExamId = new("ffd5cc6b-2f96-4456-8f1d-171ee3245b95");

    public static async Task<StudentExamAttempt?> FindBestFinalAsync(
        IAppDbContext db, IQueryable<StudentExamAttempt> attempts, Guid examId, CancellationToken ct)
    {
        if (examId != LectureNineExamId && examId != LectureTenExamId && examId != LectureElevenExamId)
            return null;

        var finalizedCandidates = await attempts
            .Where(attempt => attempt.DefinitionSnapshotJson != null && attempt.SubmittedAt != null
                && !string.IsNullOrWhiteSpace(attempt.Evaluation)
                && attempt.Evaluation != ExamAccessPolicy.PendingReviewEvaluation
                && !db.EssaySubmissions.Any(essay => essay.StudentExamAttemptId == attempt.Id
                    && essay.Status != EssaySubmissionStatus.TeacherGraded))
            .OrderByDescending(attempt => attempt.SubmittedAt)
            .ThenByDescending(attempt => attempt.Id)
            .ToListAsync(ct);

        StudentExamAttempt? bestAttempt = null;
        decimal bestScore = decimal.MinValue;
        foreach (var attempt in finalizedCandidates)
        {
            var definition = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", examId);
            if (definition.Revision is { RequiresCompletion: true } or { RequiresReview: true })
                continue;

            var score = attempt.ScoreAchieved;
            if (!definition.UsesAssignedQuestionPoints && definition.ReserveQuestions.Length > 0)
            {
                var projected = await new GetExamAttemptResultQueryHandler(db)
                    .Handle(new(attempt.Id, attempt.UserId), ct);
                score = projected.Data?.ScoreAchieved ?? score;
            }

            if (score <= bestScore)
                continue;

            bestAttempt = attempt;
            bestScore = score;
        }

        return bestAttempt;
    }
}
