using MediatR;
using NaderGorge.Application.Features.Assessments;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Homework;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Homework.Queries;

// DTOs
public record HomeworkResultDto(
    Guid HomeworkId,
    Guid SubmissionId,
    string Title,
    decimal? Score,
    decimal? TotalScore,
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
    List<HomeworkQuestionReviewDto> QuestionReviews,
    PartialHomeworkResultDto? PartialResult = null,
    bool ScoreVisible = true,
    bool AnswersVisible = true,
    bool AllowRetake = true
);

public record PartialHomeworkResultDto(decimal? ScoreAchieved, decimal? TotalScore, int PendingQuestionCount);

public record HomeworkQuestionReviewDto(
    Guid QuestionId,
    int Order,
    int QuestionType,
    string Text,
    string? ProvidedAnswer,
    string? CorrectAnswer,
    int MaxPoints,
    decimal? ScoreReceived,
    bool? IsCorrect,
    string? WrittenCorrection,
    string? AudioUrl,
    string? ImageUrl,
    string[]? PossibleAnswers,
    string? GradingFeedback = null,
    bool IsExcludedFromScore = false
);

// Query
public record GetHomeworkResultQuery(Guid HomeworkId, Guid StudentId) : IRequest<ApiResponse<HomeworkResultDto>>;

// Handler
public class GetHomeworkResultQueryHandler : IRequestHandler<GetHomeworkResultQuery, ApiResponse<HomeworkResultDto>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IAccessCheckService _access;
    private readonly IContentArchiveAccessService _archiveAccess;

    public GetHomeworkResultQueryHandler(
        IAppDbContext dbContext,
        IAccessCheckService access,
        IContentArchiveAccessService? archiveAccess = null)
    {
        _dbContext = dbContext;
        _access = access;
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(dbContext);
    }

    public async Task<ApiResponse<HomeworkResultDto>> Handle(GetHomeworkResultQuery request, CancellationToken ct)
    {
        var homework = await _dbContext.Homeworks
            .ReadyForStudents()
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.Id == request.HomeworkId, ct);

        if (homework == null)
            return ApiResponse<HomeworkResultDto>.Fail("هذا الواجب غير متاح للطلاب حاليًا.");
        if (!await _archiveAccess.CanViewAsync(
                request.StudentId,
                ContentArchiveTargetType.Homework,
                homework.Id,
                ct))
        {
            return ApiResponse<HomeworkResultDto>.Fail(
                "هذا الواجب مؤرشف وغير متاح لحسابك.",
                ["CONTENT_ARCHIVED"]);
        }

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

        homework = AssessmentDefinitionSnapshot.ResolveHomework(homework, submission.DefinitionSnapshotJson);

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

            decimal? scoreReceived = answer?.AwardedScore
                ?? (question.QuestionType != QuestionType.Essay || submission.Status == SubmissionStatus.Graded ? 0 : null);
            var excluded = question.QuestionType == QuestionType.Essay && homework.EssayGradingMode == ExamEssayGradingMode.ModelAnswerOnly;
            bool? isCorrect = null;
            if (!excluded && scoreReceived.HasValue)
            {
                isCorrect = scoreReceived > 0 && scoreReceived >= question.PointsActive;
                if (isCorrect.Value)
                    correctAnswers++;
                else
                    wrongAnswers++;
            }
            else if (!excluded)
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
                ScoreReceived: scoreReceived,
                IsCorrect: isCorrect,
                WrittenCorrection: question.WrittenCorrection,
                AudioUrl: question.AudioUrl,
                ImageUrl: question.ImageUrl,
                PossibleAnswers: question.PossibleAnswers,
                GradingFeedback: scoreReceived.HasValue ? EssayCreditPolicy.StudentFeedback(answer?.GradingFeedback) : null,
                IsExcludedFromScore: excluded
            ));
        }

        bool isPassed = submission.Status == SubmissionStatus.Graded
            && submission.OverallScore >= (submission.PassingScoreSnapshot ?? homework.PassingScoreThreshold ?? 0);

        bool canReview = homework.ShowAnswersToStudents
            && (submission.Status == SubmissionStatus.Graded || homework.ShowResultsWhileEssayPending);
        var rawMaximum = homework.Questions.Sum(q => q.PointsActive);
        var scale = rawMaximum > 0 ? homework.TotalScore / rawMaximum : 0;
        var gradedQuestions = questionReviews.Where(q => q.ScoreReceived.HasValue && !q.IsExcludedFromScore).ToArray();
        var partial = submission.Status == SubmissionStatus.PendingReview && homework.ShowResultsWhileEssayPending
            ? new PartialHomeworkResultDto(
                homework.ShowScoreToStudents ? Math.Round(gradedQuestions.Sum(q => q.ScoreReceived!.Value) * scale, 2) : null,
                homework.ShowScoreToStudents ? Math.Round(gradedQuestions.Sum(q => q.MaxPoints) * scale, 2) : null, ungradedAnswers)
            : null;

        var result = new HomeworkResultDto(
            HomeworkId: homework.Id,
            SubmissionId: submission.Id,
            Title: homework.Title,
            Score: homework.ShowScoreToStudents ? submission.OverallScore : null,
            TotalScore: homework.ShowScoreToStudents ? homework.TotalScore : null,
            PassingScore: homework.ShowScoreToStudents ? homework.PassingScoreThreshold : null,
            IsPassed: isPassed,
            Evaluation: submission.Evaluation,
            SubmittedAt: submission.SubmittedAt,
            GradedAt: submission.GradedAt,
            Status: submission.Status.ToString(),
            TotalQuestions: homework.Questions.Count,
            CorrectAnswers: canReview ? correctAnswers : 0,
            WrongAnswers: canReview ? wrongAnswers : 0,
            UngradedAnswers: ungradedAnswers,
            QuestionReviews: canReview ? questionReviews : [],
            PartialResult: partial,
            ScoreVisible: homework.ShowScoreToStudents,
            AnswersVisible: homework.ShowAnswersToStudents,
            AllowRetake: !homework.SingleAttemptOnly
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
