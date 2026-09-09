using MediatR;
using NaderGorge.Application.Features.Assessments;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public enum AssessmentKind { Homework, Exam }
public record AssessmentTarget(AssessmentKind Kind, Guid AssessmentId, Guid AttemptId, Guid ActorId);
public record AssessmentAnswerReview(Guid QuestionId, int Order, string Text, string? ImageUrl,
    string? Answer, string? AudioUrl, string? Correction, decimal Maximum, decimal? Score);
public record AssessmentReviewDto(Guid AttemptId, string StudentName, string Title, decimal Score,
    decimal Total, string Status, bool CanGrade, string? Feedback, List<AssessmentAnswerReview> Questions);
public record GetAssessmentReviewQuery(AssessmentTarget Target) : IRequest<ApiResponse<AssessmentReviewDto>>;

public static class AssessmentAccess
{
    public static async Task<bool> CanGrade(IAppDbContext db, TeacherAuthorizationService auth, AssessmentTarget target, CancellationToken ct)
    {
        if (!await Allowed(db, auth, target, ct)) return false;
        var savedDefinition = target.Kind == AssessmentKind.Homework
            ? await db.HomeworkSubmissions.Where(s => s.Id == target.AttemptId && s.HomeworkId == target.AssessmentId)
                .Select(s => s.DefinitionSnapshotJson).SingleOrDefaultAsync(ct)
            : await db.StudentExamAttempts.Where(a => a.Id == target.AttemptId && a.ExamId == target.AssessmentId)
                .Select(a => a.DefinitionSnapshotJson).SingleOrDefaultAsync(ct);
        if (savedDefinition is not null)
        {
            var containsEssay = AssessmentDefinitionSnapshot.ContainsEssay(savedDefinition,
                target.Kind == AssessmentKind.Homework ? "homework" : "exam", target.AssessmentId);
            return !containsEssay || await auth.CanAccessTeacherWorkspacePermissionAsync(target.ActorId, "essays", ct);
        }
        var hasEssay = target.Kind == AssessmentKind.Homework
            ? await db.HomeworkQuestions.AnyAsync(q => q.HomeworkId == target.AssessmentId && q.QuestionType == NaderGorge.Domain.Entities.Homework.QuestionType.Essay, ct)
            : await db.ExamQuestions.AnyAsync(q => q.ExamId == target.AssessmentId && q.Question.Type == NaderGorge.Domain.Entities.QuestionType.Essay, ct);
        return !hasEssay || await auth.CanAccessTeacherWorkspacePermissionAsync(target.ActorId, "essays", ct);
    }
    public static async Task<bool> Allowed(IAppDbContext db, TeacherAuthorizationService auth, AssessmentTarget target, CancellationToken ct)
    {
        if (target.Kind == AssessmentKind.Exam)
            return await auth.CanAccessExamAsync(target.ActorId, target.AssessmentId, ct);
        var lessonId = await db.Homeworks.Where(h => h.Id == target.AssessmentId).Select(h => (Guid?)h.LessonId).SingleOrDefaultAsync(ct);
        return lessonId.HasValue && await auth.CanAccessLessonAsync(target.ActorId, lessonId.Value, ct);
    }
}

public class GetAssessmentReviewQueryHandler(IAppDbContext db, TeacherAuthorizationService auth)
    : IRequestHandler<GetAssessmentReviewQuery, ApiResponse<AssessmentReviewDto>>
{
    public async Task<ApiResponse<AssessmentReviewDto>> Handle(GetAssessmentReviewQuery request, CancellationToken ct)
    {
        if (!await AssessmentAccess.Allowed(db, auth, request.Target, ct))
            return ApiResponse<AssessmentReviewDto>.Fail("غير مصرح بعرض هذه الإجابات.");
        var review = request.Target.Kind == AssessmentKind.Homework
            ? await HomeworkReview(request.Target, ct) : await ExamReview(request.Target, ct);
        if (review == null) return ApiResponse<AssessmentReviewDto>.Fail("المحاولة غير موجودة.");
        return ApiResponse<AssessmentReviewDto>.Ok(review with { CanGrade = review.CanGrade && await AssessmentAccess.CanGrade(db, auth, request.Target, ct) });
    }

    private async Task<AssessmentReviewDto?> HomeworkReview(AssessmentTarget target, CancellationToken ct)
    {
        var submission = await db.HomeworkSubmissions.AsNoTracking().Include(s => s.Student)
            .Include(s => s.Answers).Include(s => s.Homework).ThenInclude(h => h.Questions)
            .SingleOrDefaultAsync(s => s.Id == target.AttemptId && s.HomeworkId == target.AssessmentId, ct);
        if (submission == null) return null;
        var definition = AssessmentDefinitionSnapshot.ResolveHomework(submission.Homework, submission.DefinitionSnapshotJson);
        var questions = definition.Questions.OrderBy(q => q.Order).Select(q =>
        {
            var answer = submission.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
            return new AssessmentAnswerReview(q.Id, q.Order, q.BodyText, q.ImageUrl, answer?.ProvidedAnswer,
                null, q.WrittenCorrection ?? q.CorrectAnswerKey, q.PointsActive, answer?.ScoreReceived);
        }).ToList();
        return new(submission.Id, submission.Student.FullName, definition.Title, submission.OverallScore,
            definition.TotalScore, submission.Status.ToString(), submission.SubmittedAt != null, submission.AssistantNotes, questions);
    }

    private async Task<AssessmentReviewDto?> ExamReview(AssessmentTarget target, CancellationToken ct)
    {
        var attempt = await db.StudentExamAttempts.AsNoTracking().Include(a => a.User)
            .Include(a => a.Answers).ThenInclude(a => a.SelectedOption)
            .Include(a => a.Exam).ThenInclude(e => e.ExamQuestions).ThenInclude(q => q.Question).ThenInclude(q => q.Options)
            .SingleOrDefaultAsync(a => a.Id == target.AttemptId && a.ExamId == target.AssessmentId, ct);
        if (attempt == null) return null;
        var definition = AssessmentDefinitionSnapshot.ResolveExam(attempt.Exam, attempt.DefinitionSnapshotJson);
        var essays = await db.EssaySubmissions.AsNoTracking().Where(e => e.StudentExamAttemptId == attempt.Id).ToListAsync(ct);
        var assignedIds = attempt.Answers.Select(a => a.ExamQuestionId).ToHashSet();
        var questions = definition.ExamQuestions.Where(q => assignedIds.Contains(q.Id)).OrderBy(q => q.Order).Select(q =>
        {
            var answer = attempt.Answers.FirstOrDefault(a => a.ExamQuestionId == q.Id);
            var essay = essays.Where(e => e.QuestionId == q.QuestionBankItemId).OrderByDescending(e => e.UpdatedAt ?? e.CreatedAt).FirstOrDefault();
            return new AssessmentAnswerReview(q.Id, q.Order, q.Question.Text, q.Question.ImageUrl,
                essay?.AnswerText ?? (q.Question.Type == NaderGorge.Domain.Entities.QuestionType.MCQ
                    ? q.Question.Options.FirstOrDefault(o => o.Id == answer?.SelectedOptionId)?.Text : null)
                    ?? answer?.SubmittedText ?? answer?.SelectedOption?.Text, essay?.AudioUrl,
                q.Question.WrittenCorrection ?? q.Question.Options.FirstOrDefault(o => o.IsCorrect)?.Text,
                q.Points, essay != null ? essay.TeacherFinalScore : answer?.PointsAwarded);
        }).ToList();
        var pending = essays.Any(e => e.Status != NaderGorge.Domain.Entities.EssaySubmissionStatus.TeacherGraded);
        return new(attempt.Id, attempt.User.FullName, definition.Title, attempt.ScoreAchieved, definition.TotalScore,
            pending ? "PendingReview" : attempt.Evaluation ?? "InProgress", attempt.Evaluation != null || essays.Count > 0, null, questions);
    }
}
