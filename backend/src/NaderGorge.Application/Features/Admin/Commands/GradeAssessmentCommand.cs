using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;
using System.Text.Json;

namespace NaderGorge.Application.Features.Admin.Commands;

public record AssessmentScoreInput(Guid QuestionId, decimal Score);
public record GradeAssessmentCommand(AssessmentTarget Target, List<AssessmentScoreInput> Scores, string? Feedback)
    : IRequest<ApiResponse<bool>>;

public class GradeAssessmentCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    : IRequestHandler<GradeAssessmentCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(GradeAssessmentCommand request, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, retryCt);
            if (!await AssessmentAccess.CanGrade(db, auth, request.Target, retryCt))
                return ApiResponse<bool>.Fail("غير مصرح بتصحيح هذه المحاولة.");
            if (request.Feedback?.Length > 4000 || request.Scores.Count == 0 || request.Scores.Count > 500 ||
                request.Scores.Select(s => s.QuestionId).Distinct().Count() != request.Scores.Count)
                return ApiResponse<bool>.Fail("راجع درجات الأسئلة وملاحظات التصحيح.");
            var response = request.Target.Kind == AssessmentKind.Homework
                ? await GradeHomework(request, retryCt) : await GradeExam(request, retryCt);
            if (!response.Success) return response;
            db.AuditLogs.Add(new AuditLog
            {
                Action = "AssessmentManuallyGraded", EntityType = request.Target.Kind.ToString(), EntityId = request.Target.AttemptId,
                PerformedByUserId = request.Target.ActorId, NewValues = JsonSerializer.Serialize(request.Scores)
            });
            await db.SaveChangesAsync(retryCt);
            await transaction.CommitAsync(retryCt);
            return response;
        }, ct);

    private static bool ValidScores(List<AssessmentScoreInput> scores, Dictionary<Guid, decimal> maximums) =>
        scores.Count == maximums.Count && scores.All(s => maximums.TryGetValue(s.QuestionId, out var maximum) && s.Score >= 0 && s.Score <= maximum);

    private async Task<ApiResponse<bool>> GradeHomework(GradeAssessmentCommand request, CancellationToken ct)
    {
        var submission = await db.HomeworkSubmissions.Include(s => s.Answers).Include(s => s.Homework).ThenInclude(h => h.Questions)
            .SingleOrDefaultAsync(s => s.Id == request.Target.AttemptId && s.HomeworkId == request.Target.AssessmentId, ct);
        if (submission?.SubmittedAt == null) return ApiResponse<bool>.Fail("الواجب غير موجود أو لم يتم تسليمه.");
        var maximums = submission.Homework.Questions.ToDictionary(q => q.Id, q => (decimal)q.PointsActive);
        if (!ValidScores(request.Scores, maximums) || request.Scores.Any(s => s.Score != decimal.Truncate(s.Score)))
            return ApiResponse<bool>.Fail("أدخل درجة صحيحة لكل سؤال بين صفر والحد الأقصى.");
        foreach (var score in request.Scores)
        {
            var answer = submission.Answers.FirstOrDefault(a => a.QuestionId == score.QuestionId);
            if (answer == null)
            {
                answer = new HomeworkAnswer { HomeworkSubmissionId = submission.Id, QuestionId = score.QuestionId, ProvidedAnswer = "" };
                submission.Answers.Add(answer);
                db.HomeworkAnswers.Add(answer);
            }
            answer.ScoreReceived = (int)score.Score;
        }
        submission.OverallScore = GradingEvaluationService.CalculateScaledScore(request.Scores.Sum(s => s.Score), maximums.Values.Sum(), submission.Homework.TotalScore);
        submission.Evaluation = GradingEvaluationService.DetermineEvaluation(submission.OverallScore, submission.Homework.PassingScoreThreshold ?? 0, submission.Homework.TotalScore);
        submission.Status = SubmissionStatus.Graded;
        submission.GradedAt = DateTime.UtcNow;
        submission.AssistantReviewerId = request.Target.ActorId;
        submission.AssistantNotes = request.Feedback;
        Notify("HomeworkGraded", submission.StudentId, new { homeworkId = submission.HomeworkId, submissionId = submission.Id, score = submission.OverallScore });
        return ApiResponse<bool>.Ok(true);
    }

    private async Task<ApiResponse<bool>> GradeExam(GradeAssessmentCommand request, CancellationToken ct)
    {
        var attempt = await db.StudentExamAttempts.Include(a => a.Answers).Include(a => a.Exam).ThenInclude(e => e.ExamQuestions).ThenInclude(q => q.Question)
            .SingleOrDefaultAsync(a => a.Id == request.Target.AttemptId && a.ExamId == request.Target.AssessmentId, ct);
        if (attempt == null) return ApiResponse<bool>.Fail("المحاولة غير موجودة.");
        var essays = await db.EssaySubmissions.Where(e => e.StudentExamAttemptId == attempt.Id).ToListAsync(ct);
        if (attempt.Evaluation == null && essays.Count == 0) return ApiResponse<bool>.Fail("لم يتم تسليم الامتحان.");
        var assignedIds = attempt.Answers.Select(a => a.ExamQuestionId).ToHashSet();
        var maximums = attempt.Exam.ExamQuestions.Where(q => assignedIds.Contains(q.Id)).ToDictionary(q => q.Id, q => q.Points);
        if (!ValidScores(request.Scores, maximums)) return ApiResponse<bool>.Fail("أدخل درجة لكل سؤال بين صفر والحد الأقصى.");
        var teacherId = await db.TeacherProfiles.Where(t => t.UserId == request.Target.ActorId).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        foreach (var score in request.Scores)
        {
            var question = attempt.Exam.ExamQuestions.Single(q => q.Id == score.QuestionId);
            var answer = attempt.Answers.Single(a => a.ExamQuestionId == question.Id);
            answer.PointsAwarded = score.Score;
            answer.IsCorrect = score.Score >= question.Points;
            foreach (var essay in essays.Where(e => e.QuestionId == question.QuestionBankItemId))
            {
                essay.TeacherFinalScore = score.Score;
                essay.TeacherFeedback = request.Feedback;
                essay.GradedByTeacherId = teacherId;
                essay.Status = EssaySubmissionStatus.TeacherGraded;
            }
        }
        attempt.ScoreAchieved = GradingEvaluationService.CalculateScaledScore(request.Scores.Sum(s => s.Score), maximums.Values.Sum(), attempt.Exam.TotalScore);
        attempt.IsPassed = !attempt.IsTimeExpired && attempt.ScoreAchieved >= attempt.Exam.PassingScore;
        attempt.Evaluation = GradingEvaluationService.DetermineEvaluation(attempt.ScoreAchieved, attempt.Exam.PassingScore, attempt.Exam.TotalScore);
        var notification = new { examId = attempt.ExamId, attemptId = attempt.Id, isPassed = attempt.IsPassed, score = attempt.ScoreAchieved, evaluation = attempt.Evaluation };
        Notify("ExamGraded", attempt.UserId, notification);
        Notify("ExamResultReady", attempt.UserId, notification);
        return ApiResponse<bool>.Ok(true);
    }

    private void Notify(string type, Guid studentId, object payload) => db.OutboxEvents.Add(new OutboxEvent
    {
        Type = type, TargetUserId = studentId.ToString(), PayloadJson = JsonSerializer.Serialize(payload)
    });
}
