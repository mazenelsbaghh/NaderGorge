using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Webhooks.Commands;

public record WebhookEssayGradedResultDto(Guid EssaySubmissionId, string Status);

public record WebhookEssayGradedCommand(Guid EssaySubmissionId, decimal AiScore, string? AiFeedback)
    : IRequest<ApiResponse<WebhookEssayGradedResultDto>>;

public class WebhookEssayGradedCommandHandler
    : IRequestHandler<WebhookEssayGradedCommand, ApiResponse<WebhookEssayGradedResultDto>>
{
    private readonly IAppDbContext _db;

    public WebhookEssayGradedCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<WebhookEssayGradedResultDto>> Handle(WebhookEssayGradedCommand request, CancellationToken ct)
    {
        var submission = await _db.EssaySubmissions.FindAsync(new object[] { request.EssaySubmissionId }, ct);
        if (submission == null)
        {
            return ApiResponse<WebhookEssayGradedResultDto>.Fail("Essay submission not found.");
        }

        if (submission.Status != EssaySubmissionStatus.WaitAI)
        {
            return ApiResponse<WebhookEssayGradedResultDto>.Ok(
                new WebhookEssayGradedResultDto(submission.Id, submission.Status.ToString()),
                "Essay submission has already processed or left WaitAI state.");
        }

        var attempt = await _db.StudentExamAttempts
            .FirstOrDefaultAsync(a => a.Id == submission.StudentExamAttemptId, ct);
        if (attempt == null)
        {
            return ApiResponse<WebhookEssayGradedResultDto>.Fail("Exam attempt not found.");
        }

        var exam = await _db.Exams
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.Id == attempt.ExamId, ct);
        if (exam == null)
        {
            return ApiResponse<WebhookEssayGradedResultDto>.Fail("Exam not found.");
        }

        var examQuestion = exam.ExamQuestions.FirstOrDefault(eq => eq.QuestionBankItemId == submission.QuestionId);
        if (examQuestion == null)
        {
            return ApiResponse<WebhookEssayGradedResultDto>.Fail("Essay question is not linked to this exam.");
        }

        var isCorrect = request.AiScore >= 1m;
        var awardedScore = isCorrect ? examQuestion.Points : 0m;

        submission.AiInitialScore = request.AiScore;
        submission.AiFeedback = request.AiFeedback;
        submission.TeacherFinalScore = awardedScore;
        submission.TeacherFeedback = request.AiFeedback;
        submission.Status = EssaySubmissionStatus.TeacherGraded;

        var studentAnswer = await _db.StudentAnswers
            .FirstOrDefaultAsync(a => a.StudentExamAttemptId == attempt.Id && a.ExamQuestionId == examQuestion.Id, ct);
        if (studentAnswer != null)
        {
            studentAnswer.IsCorrect = isCorrect;
            studentAnswer.PointsAwarded = awardedScore;
        }

        await CompleteAttemptIfAllEssaysGradedAsync(submission, attempt, exam, ct);

        await _db.SaveChangesAsync(ct);

        return ApiResponse<WebhookEssayGradedResultDto>.Ok(
            new WebhookEssayGradedResultDto(submission.Id, submission.Status.ToString()));
    }

    private async Task CompleteAttemptIfAllEssaysGradedAsync(
        EssaySubmission currentSubmission,
        StudentExamAttempt attempt,
        Exam exam,
        CancellationToken ct)
    {
        var essaySubmissions = await _db.EssaySubmissions
            .Where(e => e.StudentExamAttemptId == attempt.Id)
            .ToListAsync(ct);

        var latestEssaySubmissions = essaySubmissions
            .GroupBy(e => e.QuestionId)
            .Select(g => g
                .OrderByDescending(e => e.UpdatedAt ?? e.CreatedAt)
                .First())
            .ToList();

        if (latestEssaySubmissions.Any(e => e.Status != EssaySubmissionStatus.TeacherGraded))
        {
            return;
        }

        var essayQuestionIds = exam.ExamQuestions
            .Where(eq => eq.Question.Type == QuestionType.Essay)
            .Select(eq => eq.Id)
            .ToHashSet();

        var objectivePointsEarned = await _db.StudentAnswers
            .Where(a => a.StudentExamAttemptId == attempt.Id && !essayQuestionIds.Contains(a.ExamQuestionId))
            .SumAsync(a => a.PointsAwarded, ct);

        var rawPointsEarned = objectivePointsEarned + latestEssaySubmissions.Sum(e => e.TeacherFinalScore ?? 0m);
        var rawPointsPossible = exam.ExamQuestions.Sum(eq => eq.Points);
        var scaledScore = GradingEvaluationService.CalculateScaledScore(rawPointsEarned, rawPointsPossible, exam.TotalScore);

        attempt.ScoreAchieved = scaledScore;
        attempt.IsPassed = !attempt.IsTimeExpired && scaledScore >= exam.PassingScore;
        attempt.Evaluation = GradingEvaluationService.DetermineEvaluation(scaledScore, exam.PassingScore, exam.TotalScore);

        _db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "ExamGraded",
            TargetUserId = currentSubmission.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                examId = attempt.ExamId,
                attemptId = attempt.Id,
                isPassed = attempt.IsPassed,
                score = attempt.ScoreAchieved,
                evaluation = attempt.Evaluation
            })
        });

        _db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "ExamResultReady",
            TargetUserId = currentSubmission.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                examId = attempt.ExamId,
                attemptId = attempt.Id,
                isPassed = attempt.IsPassed,
                score = attempt.ScoreAchieved
            })
        });

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.ExamId == exam.Id, ct);

        if (lesson == null)
        {
            return;
        }

        var progress = await _db.LessonProgresses
            .FirstOrDefaultAsync(lp => lp.UserId == currentSubmission.StudentId && lp.LessonId == lesson.Id, ct);
        if (progress == null)
        {
            _db.LessonProgresses.Add(new LessonProgress
            {
                UserId = currentSubmission.StudentId,
                LessonId = lesson.Id,
                IsCompleted = attempt.IsPassed,
                IsManuallyUnlocked = false
            });
        }
        else if (attempt.IsPassed)
        {
            progress.IsCompleted = true;
        }

        var nextLesson = await _db.Lessons
            .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order > lesson.Order)
            .OrderBy(l => l.Order)
            .FirstOrDefaultAsync(ct);

        if (nextLesson == null)
        {
            var nextSection = await _db.ContentSections
                .Where(s => s.TermId == lesson.ContentSection.TermId && s.Order > lesson.ContentSection.Order)
                .OrderBy(s => s.Order)
                .FirstOrDefaultAsync(ct);

            if (nextSection != null)
            {
                nextLesson = await _db.Lessons
                    .Where(l => l.ContentSectionId == nextSection.Id)
                    .OrderBy(l => l.Order)
                    .FirstOrDefaultAsync(ct);
            }
        }

        if (nextLesson == null)
        {
            return;
        }

        var nextLessonProgress = await _db.LessonProgresses
            .FirstOrDefaultAsync(lp => lp.UserId == currentSubmission.StudentId && lp.LessonId == nextLesson.Id, ct);
        var nextIsLocked = !attempt.IsPassed && (nextLessonProgress == null || !nextLessonProgress.IsManuallyUnlocked);

        _db.OutboxEvents.Add(new OutboxEvent
        {
            Type = nextIsLocked ? "LessonLocked" : "LessonUnlocked",
            TargetUserId = currentSubmission.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = nextLesson.Id,
                reason = nextIsLocked ? $"يجب اجتياز امتحان '{exam.Title}' بنجاح." : null
            })
        });
    }
}
