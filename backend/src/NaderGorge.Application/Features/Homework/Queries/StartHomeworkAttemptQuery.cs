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

        // If existing Graded submission → return result with AlreadyCompleted = true
        if (submission != null && submission.Status == SubmissionStatus.Graded)
        {
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
                Questions: questions
            ), "Homework already completed.");
        }

        // If existing InProgress or PendingReview submission → reuse it
        if (submission != null && (submission.Status == SubmissionStatus.InProgress || submission.Status == SubmissionStatus.PendingReview))
        {
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
                Questions: questions
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
            Questions: questions
        ));
    }
}
