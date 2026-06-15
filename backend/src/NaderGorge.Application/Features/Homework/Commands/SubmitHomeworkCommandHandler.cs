using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Homework.Commands;

public class SubmitHomeworkCommandHandler : IRequestHandler<SubmitHomeworkCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IPublisher _publisher;
    private readonly IAccessCheckService _access;

    public SubmitHomeworkCommandHandler(IAppDbContext dbContext, IPublisher publisher, IAccessCheckService access)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _access = access;
    }

    public async Task<ApiResponse<bool>> Handle(SubmitHomeworkCommand request, CancellationToken cancellationToken)
    {
        var homework = await _dbContext.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.Id == request.HomeworkId, cancellationToken);

        if (homework == null)
            return ApiResponse<bool>.Fail("Homework not found");

        var hasAccess = await _access.HasAccessToLessonAsync(request.StudentId, homework.LessonId, cancellationToken);
        if (!hasAccess)
        {
            return ApiResponse<bool>.Fail("You do not have access to this homework's lesson.");
        }

        var lesson = await _dbContext.Lessons.FirstOrDefaultAsync(l => l.Id == homework.LessonId, cancellationToken);
        if (lesson != null)
        {
            var previousLesson = await _dbContext.Lessons
                .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order < lesson.Order)
                .OrderByDescending(l => l.Order)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousLesson != null)
            {
                // 1. Previous exam
                if (previousLesson.ExamId.HasValue)
                {
                    var exam = await _dbContext.Exams.FindAsync(new object[] { previousLesson.ExamId.Value }, cancellationToken);
                    if (exam != null && exam.IsMandatory)
                    {
                        var passedExam = await _dbContext.StudentExamAttempts
                            .AnyAsync(a => a.UserId == request.StudentId && a.ExamId == previousLesson.ExamId.Value && a.IsPassed, cancellationToken);

                        if (!passedExam)
                        {
                            return ApiResponse<bool>.Fail("Previous lesson's exam is not passed.");
                        }
                    }
                }

                // 2. Previous homework
                var prevHomework = await _dbContext.Homeworks.FirstOrDefaultAsync(h => h.LessonId == previousLesson.Id, cancellationToken);
                if (prevHomework != null && prevHomework.IsMandatory)
                {
                    var prevHwSubmission = await _dbContext.HomeworkSubmissions
                        .Where(s => s.StudentId == request.StudentId && s.HomeworkId == prevHomework.Id)
                        .OrderByDescending(s => s.SubmittedAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    bool prevHwPassed = prevHwSubmission != null 
                                      && prevHwSubmission.Status == SubmissionStatus.Graded 
                                      && prevHwSubmission.OverallScore >= (prevHomework.PassingScoreThreshold ?? 0);

                    if (!prevHwPassed)
                    {
                        return ApiResponse<bool>.Fail("Previous lesson's homework is not passed.");
                    }
                }
            }

            // 3. Current lesson's exam
            if (lesson.ExamId.HasValue)
            {
                var currentExam = await _dbContext.Exams.FindAsync(new object[] { lesson.ExamId.Value }, cancellationToken);
                if (currentExam != null && currentExam.IsMandatory)
                {
                    var passedCurrentExam = await _dbContext.StudentExamAttempts
                        .AnyAsync(a => a.UserId == request.StudentId && a.ExamId == lesson.ExamId.Value && a.IsPassed, cancellationToken);

                    if (!passedCurrentExam)
                    {
                        return ApiResponse<bool>.Fail("Current lesson's exam is not passed.");
                    }
                }
            }
        }

        // Check if a submission already exists
        var submission = await _dbContext.HomeworkSubmissions
            .FirstOrDefaultAsync(s => s.HomeworkId == request.HomeworkId && s.StudentId == request.StudentId, cancellationToken);

        if (submission != null && submission.Status != SubmissionStatus.InProgress)
        {
            return ApiResponse<bool>.Fail("Homework already submitted.");
        }

        if (submission == null)
        {
            submission = new HomeworkSubmission
            {
                Id = Guid.NewGuid(),
                HomeworkId = request.HomeworkId,
                StudentId = request.StudentId,
                StartedAt = DateTime.UtcNow
            };
            _dbContext.HomeworkSubmissions.Add(submission);
        }
        else
        {
            // Remove existing answers to replace them.
            var existingAnswers = await _dbContext.HomeworkAnswers
                .Where(a => a.HomeworkSubmissionId == submission.Id)
                .ToListAsync(cancellationToken);
            _dbContext.HomeworkAnswers.RemoveRange(existingAnswers);
        }

        // Process answers.
        var questionLookup = homework.Questions.ToDictionary(q => q.Id);
        decimal rawPointsEarned = 0;
        decimal rawPointsPossible = 0;
        bool hasEssayQuestions = false;

        foreach (var answerInput in request.Answers)
        {
            if (!questionLookup.TryGetValue(answerInput.QuestionId, out var question))
                continue;

            rawPointsPossible += question.PointsActive;

            var answer = new HomeworkAnswer
            {
                Id = Guid.NewGuid(),
                HomeworkSubmissionId = submission.Id,
                QuestionId = answerInput.QuestionId,
                ProvidedAnswer = answerInput.ProvidedAnswer
            };

            switch (question.QuestionType)
            {
                case QuestionType.MCQ:
                {
                    // Auto-grade MCQ: compare provided answer with CorrectAnswerKey
                    var isCorrect = !string.IsNullOrWhiteSpace(question.CorrectAnswerKey) &&
                        string.Equals(answerInput.ProvidedAnswer?.Trim(), question.CorrectAnswerKey.Trim(), StringComparison.OrdinalIgnoreCase);
                    answer.ScoreReceived = isCorrect ? question.PointsActive : 0;
                    if (isCorrect) rawPointsEarned += question.PointsActive;
                    break;
                }
                case QuestionType.FindTheMistake:
                {
                    // Auto-grade FindTheMistake: compare selected text with BaseText[MistakeStartIndex..MistakeEndIndex]
                    string? correctText = null;
                    if (!string.IsNullOrEmpty(question.BaseText) &&
                        question.MistakeStartIndex.HasValue &&
                        question.MistakeEndIndex.HasValue &&
                        question.MistakeStartIndex.Value >= 0 &&
                        question.MistakeEndIndex.Value <= question.BaseText.Length &&
                        question.MistakeStartIndex.Value < question.MistakeEndIndex.Value)
                    {
                        correctText = question.BaseText[question.MistakeStartIndex.Value..question.MistakeEndIndex.Value];
                    }

                    var isCorrect = !string.IsNullOrWhiteSpace(answerInput.ProvidedAnswer) &&
                        !string.IsNullOrWhiteSpace(correctText) &&
                        string.Equals(answerInput.ProvidedAnswer.Trim(), correctText.Trim(), StringComparison.Ordinal);
                    answer.ScoreReceived = isCorrect ? question.PointsActive : 0;
                    if (isCorrect) rawPointsEarned += question.PointsActive;
                    break;
                }
                case QuestionType.Essay:
                {
                    // Essay questions: leave as null (pending manual review)
                    answer.ScoreReceived = null;
                    hasEssayQuestions = true;
                    break;
                }
            }

            _dbContext.HomeworkAnswers.Add(answer);
        }

        // Calculate overall score using GradingEvaluationService
        var scaledScore = GradingEvaluationService.CalculateScaledScore(rawPointsEarned, rawPointsPossible, homework.TotalScore);
        submission.OverallScore = scaledScore;
        submission.Evaluation = hasEssayQuestions
            ? "قيد التصحيح"
            : GradingEvaluationService.DetermineEvaluation(scaledScore, homework.PassingScoreThreshold ?? 0, homework.TotalScore);

        submission.SubmittedAt = DateTime.UtcNow;

        if (hasEssayQuestions)
        {
            // Has essays → status = PendingReview (needs manual/AI grading)
            submission.Status = SubmissionStatus.PendingReview;
        }
        else
        {
            // All questions are auto-gradable → status = Graded
            submission.Status = SubmissionStatus.Graded;
            submission.GradedAt = DateTime.UtcNow;
        }

        var outboxEvent = new NaderGorge.Domain.Entities.OutboxEvent
        {
            Type = "HomeworkSubmitted",
            TargetUserId = request.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                homeworkId = request.HomeworkId,
                submissionId = submission.Id,
                studentId = request.StudentId
            })
        };
        _dbContext.OutboxEvents.Add(outboxEvent);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_HomeworkSubmissions_HomeworkId_StudentId") ||
                msg.Contains("unique constraint") ||
                msg.Contains("UNIQUE constraint") ||
                msg.Contains("duplicate key"))
            {
                var existing = await _dbContext.HomeworkSubmissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.HomeworkId == request.HomeworkId && s.StudentId == request.StudentId, cancellationToken);
                if (existing != null)
                {
                    return ApiResponse<bool>.Ok(true, "Homework already submitted.");
                }
            }
            throw;
        }

        await _publisher.Publish(new NaderGorge.Application.Features.Gamification.Commands.AcademicTaskCompletedEvent(request.StudentId, NaderGorge.Domain.Entities.Gamification.GamificationEventType.HomeworkSubmittedOnTime, 20), cancellationToken);

        return ApiResponse<bool>.Ok(true, "Homework submitted successfully.");
    }
}
