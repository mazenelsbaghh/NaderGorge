using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Homework.Queries;

// DTOs
public record StartHomeworkAttemptDto(
    Guid HomeworkId,
    Guid SubmissionId,
    string Title,
    string? Instructions,
    decimal TotalScore,
    decimal? PassingScore,
    bool AlreadyCompleted,
    decimal? Score,
    string? Evaluation,
    DateTime StartedAt,
    int? DurationMinutes,
    int? RemainingSeconds,
    List<HomeworkQuestionDto> Questions
);

public record HomeworkQuestionDto(
    Guid Id,
    int Order,
    int QuestionType, // 0=MCQ, 1=Essay, 2=FindTheMistake
    string Text,
    int MaxPoints,
    string[]? PossibleAnswers,
    string? AudioUrl,
    string? HintText,
    string? BaseText,
    int? MistakeStartIndex,
    int? MistakeEndIndex
    // NO CorrectAnswerKey! NO WrittenCorrection!
);

// Query
public record StartHomeworkAttemptQuery(Guid HomeworkId, Guid StudentId) : IRequest<ApiResponse<StartHomeworkAttemptDto>>;

// Handler
public class StartHomeworkAttemptQueryHandler : IRequestHandler<StartHomeworkAttemptQuery, ApiResponse<StartHomeworkAttemptDto>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IAccessCheckService _access;

    public StartHomeworkAttemptQueryHandler(IAppDbContext dbContext, IAccessCheckService access)
    {
        _dbContext = dbContext;
        _access = access;
    }

    public async Task<ApiResponse<StartHomeworkAttemptDto>> Handle(StartHomeworkAttemptQuery request, CancellationToken ct)
    {
        var homework = await _dbContext.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.Id == request.HomeworkId, ct);

        if (homework == null)
            return ApiResponse<StartHomeworkAttemptDto>.Fail("Homework not found.");

        // Verify student has access to the lesson
        var hasAccess = await _access.HasAccessToLessonAsync(request.StudentId, homework.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<StartHomeworkAttemptDto>.Fail("You do not have access to this homework's lesson.");

        // Find the previous lesson of this homework's lesson and enforce locks
        var lesson = await _dbContext.Lessons.FirstOrDefaultAsync(l => l.Id == homework.LessonId, ct);
        if (lesson != null)
        {
            var previousLesson = await _dbContext.Lessons
                .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order < lesson.Order)
                .OrderByDescending(l => l.Order)
                .FirstOrDefaultAsync(ct);

            if (previousLesson != null)
            {
                // 1. Previous exam
                if (previousLesson.ExamId.HasValue)
                {
                    var exam = await _dbContext.Exams.FindAsync(new object[] { previousLesson.ExamId.Value }, ct);
                    if (exam != null && exam.IsMandatory)
                    {
                        var passedExam = await _dbContext.StudentExamAttempts
                            .AnyAsync(a => a.UserId == request.StudentId && a.ExamId == previousLesson.ExamId.Value && a.IsPassed, ct);

                        if (!passedExam)
                        {
                            var attemptedExam = await _dbContext.StudentExamAttempts
                                .AnyAsync(a => a.UserId == request.StudentId && a.ExamId == previousLesson.ExamId.Value, ct);

                            string reason = attemptedExam
                                ? $"يجب اجتياز امتحان '{exam.Title}' التابع للحصة '{previousLesson.Title}' بنجاح أولاً قبل بدء هذا الواجب."
                                : $"يجب حل امتحان '{exam.Title}' التابع للحصة '{previousLesson.Title}' أولاً قبل بدء هذا الواجب.";

                            return ApiResponse<StartHomeworkAttemptDto>.Fail(reason);
                        }
                    }
                }

                // 2. Previous homework
                var prevHomework = await _dbContext.Homeworks.FirstOrDefaultAsync(h => h.LessonId == previousLesson.Id, ct);
                if (prevHomework != null && prevHomework.IsMandatory)
                {
                    var prevHwSubmission = await _dbContext.HomeworkSubmissions
                        .Where(s => s.StudentId == request.StudentId && s.HomeworkId == prevHomework.Id)
                        .OrderByDescending(s => s.SubmittedAt)
                        .FirstOrDefaultAsync(ct);

                    bool prevHwPassed = prevHwSubmission != null 
                                      && prevHwSubmission.Status == SubmissionStatus.Graded 
                                      && prevHwSubmission.OverallScore >= (prevHomework.PassingScoreThreshold ?? 0);

                    if (!prevHwPassed)
                    {
                        string reason = prevHwSubmission == null
                            ? $"يجب حل واجب الحصة السابقة '{prevHomework.Title}' أولاً قبل بدء هذا الواجب."
                            : $"يجب اجتياز واجب الحصة السابقة '{prevHomework.Title}' أولاً قبل بدء هذا الواجب.";

                        return ApiResponse<StartHomeworkAttemptDto>.Fail(reason);
                    }
                }
            }
                // 3. Current lesson's exam
                if (lesson.ExamId.HasValue)
                {
                    var currentExam = await _dbContext.Exams.FindAsync(new object[] { lesson.ExamId.Value }, ct);
                    if (currentExam != null && currentExam.IsMandatory)
                    {
                        var passedCurrentExam = await _dbContext.StudentExamAttempts
                            .AnyAsync(a => a.UserId == request.StudentId && a.ExamId == lesson.ExamId.Value && a.IsPassed, ct);

                        if (!passedCurrentExam)
                        {
                            var attemptedCurrentExam = await _dbContext.StudentExamAttempts
                                .AnyAsync(a => a.UserId == request.StudentId && a.ExamId == lesson.ExamId.Value, ct);

                            string reason = attemptedCurrentExam
                                ? $"يجب اجتياز امتحان الحصة الحالية '{currentExam.Title}' بنجاح أولاً قبل بدء هذا الواجب."
                                : $"يجب حل امتحان الحصة الحالية '{currentExam.Title}' أولاً قبل بدء هذا الواجب.";

                            return ApiResponse<StartHomeworkAttemptDto>.Fail(reason);
                        }
                }
            }
        }

        // Check for existing submission
        var submission = await _dbContext.HomeworkSubmissions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.HomeworkId == request.HomeworkId && s.StudentId == request.StudentId, ct);

        // Build questions list (without correct answers for security)
        var baseQuery = homework.Questions.AsEnumerable();
        if (homework.IsRandomized)
        {
            baseQuery = baseQuery.OrderBy(_ => Guid.NewGuid());
        }
        else
        {
            baseQuery = baseQuery.OrderBy(q => q.Order);
        }

        var questions = baseQuery.Select(q => new HomeworkQuestionDto(
            q.Id,
            q.Order,
            (int)q.QuestionType,
            q.BodyText,
            q.PointsActive,
            q.PossibleAnswers,
            q.AudioUrl,
            q.HintText,
            q.BaseText,
            q.MistakeStartIndex,
            q.MistakeEndIndex
        )).ToList();

        // If existing Graded submission
        if (submission != null && submission.Status == SubmissionStatus.Graded)
        {
            var passingScore = homework.PassingScoreThreshold ?? 0;
            bool passed = submission.OverallScore >= passingScore;

            if (passed)
            {
                var durationMinutes = 30; // Default 30 minutes
                var remainingSeconds = (int)Math.Max(0, (TimeSpan.FromMinutes(durationMinutes) - (DateTime.UtcNow - submission.StartedAt)).TotalSeconds);

                return ApiResponse<StartHomeworkAttemptDto>.Ok(new StartHomeworkAttemptDto(
                    homework.Id,
                    submission.Id,
                    homework.Title,
                    homework.Description,
                    homework.TotalScore,
                    homework.PassingScoreThreshold,
                    AlreadyCompleted: true,
                    Score: submission.OverallScore,
                    Evaluation: submission.Evaluation,
                    submission.StartedAt,
                    durationMinutes,
                    remainingSeconds,
                    questions
                ), "Homework already completed.");
            }
            else
            {
                // Delete failed attempt and answers to allow retaking
                _dbContext.HomeworkSubmissions.Remove(submission);
                await _dbContext.SaveChangesAsync(ct);
                submission = null;
            }
        }

        // If existing InProgress or PendingReview submission → reuse it
        if (submission != null && (submission.Status == SubmissionStatus.InProgress || submission.Status == SubmissionStatus.PendingReview))
        {
            var durationMinutes = 30; // Default 30 minutes
            var remainingSeconds = (int)Math.Max(0, (TimeSpan.FromMinutes(durationMinutes) - (DateTime.UtcNow - submission.StartedAt)).TotalSeconds);

            return ApiResponse<StartHomeworkAttemptDto>.Ok(new StartHomeworkAttemptDto(
                homework.Id,
                submission.Id,
                homework.Title,
                homework.Description,
                homework.TotalScore,
                homework.PassingScoreThreshold,
                AlreadyCompleted: submission.Status == SubmissionStatus.PendingReview,
                Score: submission.Status == SubmissionStatus.PendingReview ? submission.OverallScore : null,
                Evaluation: submission.Status == SubmissionStatus.PendingReview ? submission.Evaluation : null,
                submission.StartedAt,
                durationMinutes,
                remainingSeconds,
                questions
            ));
        }

        // No submission → create new one with status InProgress
        var newSubmission = new HomeworkSubmission
        {
            HomeworkId = request.HomeworkId,
            StudentId = request.StudentId,
            Status = SubmissionStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
        _dbContext.HomeworkSubmissions.Add(newSubmission);
        await _dbContext.SaveChangesAsync(ct);

        var defaultDurationMinutes = 30; // Default 30 minutes

        return ApiResponse<StartHomeworkAttemptDto>.Ok(new StartHomeworkAttemptDto(
            homework.Id,
            newSubmission.Id,
            homework.Title,
            homework.Description,
            homework.TotalScore,
            homework.PassingScoreThreshold,
            AlreadyCompleted: false,
            Score: null,
            Evaluation: null,
            newSubmission.StartedAt,
            defaultDurationMinutes,
            defaultDurationMinutes * 60,
            questions
        ));
    }
}
