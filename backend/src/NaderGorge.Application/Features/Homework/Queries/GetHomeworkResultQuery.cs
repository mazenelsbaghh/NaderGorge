using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Homework.Queries;

// DTOs
public record HomeworkResultDto(
    Guid HomeworkId,
    Guid SubmissionId,
    string Title,
    decimal Score,
    decimal TotalScore,
    decimal? PassingScore,
    bool IsPassed,
    string? Evaluation,
    DateTime? SubmittedAt,
    DateTime? GradedAt,
    string Status, // "Graded", "PendingReview"
    int TotalQuestions,
    int CorrectAnswers,
    int WrongAnswers,
    int UngradedAnswers,
    List<HomeworkQuestionReviewDto> QuestionReviews
);

public record HomeworkQuestionReviewDto(
    Guid QuestionId,
    int Order,
    int QuestionType,
    string Text,
    string? ProvidedAnswer,
    string? CorrectAnswer,
    int MaxPoints,
    int? ScoreReceived,
    bool? IsCorrect,
    string? WrittenCorrection,
    string? AudioUrl,
    string[]? PossibleAnswers
);

// Query
public record GetHomeworkResultQuery(Guid HomeworkId, Guid StudentId) : IRequest<ApiResponse<HomeworkResultDto>>;

// Handler
public class GetHomeworkResultQueryHandler : IRequestHandler<GetHomeworkResultQuery, ApiResponse<HomeworkResultDto>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IAccessCheckService _access;

    public GetHomeworkResultQueryHandler(IAppDbContext dbContext, IAccessCheckService access)
    {
        _dbContext = dbContext;
        _access = access;
    }

    public async Task<ApiResponse<HomeworkResultDto>> Handle(GetHomeworkResultQuery request, CancellationToken ct)
    {
        var homework = await _dbContext.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.Id == request.HomeworkId, ct);

        if (homework == null)
            return ApiResponse<HomeworkResultDto>.Fail("Homework not found.");

        var hasAccess = await _access.HasAccessToLessonAsync(request.StudentId, homework.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<HomeworkResultDto>.Fail("You do not have access to this homework's lesson.");

        // Find the latest Graded or PendingReview submission for this student
        var submission = await _dbContext.HomeworkSubmissions
            .Include(s => s.Answers)
            .Where(s => s.HomeworkId == request.HomeworkId &&
                        s.StudentId == request.StudentId &&
                        (s.Status == SubmissionStatus.Graded || s.Status == SubmissionStatus.PendingReview))
            .OrderByDescending(s => s.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        if (submission == null)
            return ApiResponse<HomeworkResultDto>.Fail("No completed submission found for this homework.");

        var questionLookup = homework.Questions.ToDictionary(q => q.Id);
        var answerLookup = submission.Answers.ToDictionary(a => a.QuestionId);

        int correctAnswers = 0;
        int wrongAnswers = 0;
        int ungradedAnswers = 0;

        var questionReviews = new List<HomeworkQuestionReviewDto>();

        foreach (var question in homework.Questions.OrderBy(q => q.Order))
        {
            answerLookup.TryGetValue(question.Id, out var answer);

            // Determine the correct answer text to display
            string? correctAnswer = question.QuestionType switch
            {
                QuestionType.MCQ => question.CorrectAnswerKey,
                QuestionType.FindTheMistake => GetFindTheMistakeCorrectText(question),
                QuestionType.Essay => question.WrittenCorrection, // Show model answer for essays
                _ => null
            };

            bool? isCorrect = null;
            if (answer != null && answer.ScoreReceived.HasValue)
            {
                isCorrect = answer.ScoreReceived > 0;
                if (isCorrect.Value)
                    correctAnswers++;
                else
                    wrongAnswers++;
            }
            else
            {
                ungradedAnswers++;
            }

            questionReviews.Add(new HomeworkQuestionReviewDto(
                QuestionId: question.Id,
                Order: question.Order,
                QuestionType: (int)question.QuestionType,
                Text: question.BodyText,
                ProvidedAnswer: answer?.ProvidedAnswer,
                CorrectAnswer: correctAnswer,
                MaxPoints: question.PointsActive,
                ScoreReceived: answer?.ScoreReceived,
                IsCorrect: isCorrect,
                WrittenCorrection: question.WrittenCorrection,
                AudioUrl: question.AudioUrl,
                PossibleAnswers: question.PossibleAnswers
            ));
        }

        bool isPassed = submission.OverallScore >= (homework.PassingScoreThreshold ?? 0);

        var result = new HomeworkResultDto(
            HomeworkId: homework.Id,
            SubmissionId: submission.Id,
            Title: homework.Title,
            Score: submission.OverallScore,
            TotalScore: homework.TotalScore,
            PassingScore: homework.PassingScoreThreshold,
            IsPassed: isPassed,
            Evaluation: submission.Evaluation,
            SubmittedAt: submission.SubmittedAt,
            GradedAt: submission.GradedAt,
            Status: submission.Status.ToString(),
            TotalQuestions: homework.Questions.Count,
            CorrectAnswers: correctAnswers,
            WrongAnswers: wrongAnswers,
            UngradedAnswers: ungradedAnswers,
            QuestionReviews: questionReviews
        );

        return ApiResponse<HomeworkResultDto>.Ok(result);
    }

    private static string? GetFindTheMistakeCorrectText(HomeworkQuestion question)
    {
        if (!string.IsNullOrEmpty(question.BaseText) &&
            question.MistakeStartIndex.HasValue &&
            question.MistakeEndIndex.HasValue &&
            question.MistakeStartIndex.Value >= 0 &&
            question.MistakeEndIndex.Value <= question.BaseText.Length &&
            question.MistakeStartIndex.Value < question.MistakeEndIndex.Value)
        {
            return question.BaseText[question.MistakeStartIndex.Value..question.MistakeEndIndex.Value];
        }
        return null;
    }
}
