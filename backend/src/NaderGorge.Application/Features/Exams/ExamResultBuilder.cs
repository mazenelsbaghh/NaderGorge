using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Features.Assessments;

namespace NaderGorge.Application.Features.Exams.Commands;

public record PartialExamResultDto(decimal ScoreAchieved, decimal TotalScore, int PendingQuestionCount);

public record ExamQuestionReviewDto(
    Guid ExamQuestionId,
    int Order,
    string QuestionText,
    string? SelectedOptionText,
    bool IsAnswered,
    bool IsCorrect,
    decimal PointsAwarded,
    string? CorrectOptionText,
    string? AudioUrl,
    string? ImageUrl,
    string? WrittenCorrection,
    string? StudentAudioUrl = null,
    string? GradingFeedback = null,
    decimal? MaximumPoints = null,
    bool IsExcludedFromScore = false,
    bool IsPendingGrading = false
);

public record QuestionReviewSnapshot(
    string? SelectedText,
    bool IsAnswered,
    bool IsCorrect,
    decimal PointsAwarded,
    string? StudentAudioUrl = null,
    string? GradingFeedback = null,
    bool IsPendingGrading = false
);

internal static class ExamResultBuilder
{
    public static decimal ApplyHintPenalty(decimal earnedPoints, bool hintUsed, decimal penaltyPercentage)
    {
        if (!hintUsed || earnedPoints <= 0 || penaltyPercentage <= 0)
        {
            return earnedPoints;
        }

        var penalizedPoints = earnedPoints - (earnedPoints * (penaltyPercentage / 100m));
        return decimal.Max(0m, penalizedPoints);
    }

    public static ExamResultDto Build(
        Exam exam,
        StudentExamAttempt attempt,
        bool blocksNextLesson,
        Guid? lessonId,
        Guid? packageId,
        IReadOnlyDictionary<Guid, QuestionReviewSnapshot> questionSnapshotsByQuestionId,
        bool revealCorrectAnswers,
        string? resultState = null)
    {
        var awardedPoints = questionSnapshotsByQuestionId.Values.Sum(answer => answer.PointsAwarded);
        var scale = AssessmentAttemptScaleNormalizer.Project(
            attempt.DefinitionSnapshotJson, attempt.ExamId, attempt.ScoreAchieved,
            attempt.IsPassed, attempt.IsTimeExpired, awardedPoints);
        exam = AssessmentDefinitionSnapshot.ResolveExam(exam, scale.Definition.ToJson());
        var questionReviews = exam.ExamQuestions
            .OrderBy(q => q.Order)
            .Select(eq =>
            {
                questionSnapshotsByQuestionId.TryGetValue(eq.Id, out var snapshot);
                var resultIsCompleted = string.Equals(resultState ?? "Completed", "Completed", StringComparison.Ordinal);
                var canRevealCorrectAnswer = revealCorrectAnswers
                    && (resultIsCompleted || exam.ShowResultsWhileEssayPending || eq.Question.Type != QuestionType.Essay);
                var correctText = GetCorrectReviewText(eq.Question);
                var excluded = eq.Question.Type == QuestionType.Essay && exam.EssayGradingMode == ExamEssayGradingMode.ModelAnswerOnly;

                return new ExamQuestionReviewDto(
                    eq.Id,
                    eq.Order,
                    eq.Question.Text,
                    snapshot?.SelectedText,
                    snapshot?.IsAnswered ?? false,
                    !excluded && (snapshot?.IsCorrect ?? false),
                    excluded ? 0 : snapshot?.PointsAwarded ?? 0,
                    canRevealCorrectAnswer ? correctText : null,
                    canRevealCorrectAnswer ? eq.Question.AudioUrl : null,
                    eq.Question.ImageUrl,
                    canRevealCorrectAnswer
                        ? eq.Question.WrittenCorrection
                        : null,
                    snapshot?.StudentAudioUrl,
                    resultIsCompleted && !excluded ? snapshot?.GradingFeedback : null,
                    eq.Points,
                    excluded,
                    !resultIsCompleted && !excluded && eq.Question.Type == QuestionType.Essay
                        && (snapshot?.IsPendingGrading ?? true)
                );
            })
            .ToList();

        return new ExamResultDto(
            attempt.Id,
            scale.ScoreAchieved,
            exam.TotalScore,
            scale.IsPassed,
            blocksNextLesson,
            attempt.Evaluation ?? "غير مقيم",
            attempt.IsTimeExpired,
            resultState ?? "Completed",
            lessonId,
            packageId,
            questionReviews,
            exam.ShowResultsWhileEssayPending && (resultState is "Pending" or "PartiallyGraded")
                ? PartialResult(exam, attempt, questionReviews) : null
        );
    }

    private static PartialExamResultDto PartialResult(Exam exam, StudentExamAttempt attempt,
        IReadOnlyList<ExamQuestionReviewDto> reviews)
    {
        var graded = reviews.Where(q => !q.IsPendingGrading && !q.IsExcludedFromScore).ToArray();
        var rawMaximum = reviews.Where(q => !q.IsExcludedFromScore).Sum(q => q.MaximumPoints ?? 0);
        var scale = rawMaximum > 0 ? exam.TotalScore / rawMaximum : 0;
        return new(attempt.IsTimeExpired ? 0 : Math.Round(graded.Sum(q => q.PointsAwarded) * scale, 2),
            Math.Round(graded.Sum(q => q.MaximumPoints ?? 0) * scale, 2), reviews.Count(q => q.IsPendingGrading));
    }

    public static Dictionary<Guid, QuestionReviewSnapshot> BuildQuestionReviewSnapshots(IEnumerable<StudentAnswer> answers, Exam exam)
    {
        var savedOptions = exam.ExamQuestions.SelectMany(q => q.Question.Options)
            .GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First().Text);
        var questionTypes = exam.ExamQuestions.ToDictionary(q => q.Id, q => q.Question.Type);
        return answers
            .GroupBy(a => a.ExamQuestionId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g.Last();
                    var selectedText = questionTypes.GetValueOrDefault(latest.ExamQuestionId) == QuestionType.MCQ
                        && latest.SelectedOptionId is Guid optionId && savedOptions.TryGetValue(optionId, out var optionText)
                        ? optionText
                        : !string.IsNullOrWhiteSpace(latest.SubmittedText) ? latest.SubmittedText : latest.SelectedOption?.Text;
                    var isAnswered = !string.IsNullOrWhiteSpace(selectedText);

                    return new QuestionReviewSnapshot(
                        selectedText,
                        isAnswered,
                        latest.IsCorrect,
                        latest.PointsAwarded,
                        null
                    );
                });
    }

    public static QuestionReviewSnapshot BuildEssayReviewSnapshot(EssaySubmission essay, decimal maximumPoints)
    {
        var isGraded = essay.Status == EssaySubmissionStatus.TeacherGraded;
        var awardedPoints = isGraded ? essay.TeacherFinalScore ?? 0m : 0m;
        return new QuestionReviewSnapshot(
            essay.AnswerText,
            !string.IsNullOrWhiteSpace(essay.AnswerText) || !string.IsNullOrWhiteSpace(essay.AudioUrl),
            isGraded && awardedPoints > 0m && awardedPoints >= maximumPoints,
            awardedPoints,
            essay.AudioUrl,
            isGraded ? essay.TeacherFeedback : null,
            IsPendingGrading: !isGraded);
    }

    public static string? GetCorrectReviewText(QuestionBankItem question)
    {
        if (question is FindTheMistakeQuestion findTheMistake)
        {
            var start = Math.Clamp(findTheMistake.MistakeStartIndex, 0, findTheMistake.BaseText.Length);
            var end = Math.Clamp(findTheMistake.MistakeEndIndex, start, findTheMistake.BaseText.Length);
            if (end > start)
            {
                return findTheMistake.BaseText[start..end];
            }
        }

        return question.Options.FirstOrDefault(o => o.IsCorrect)?.Text ?? question.WrittenCorrection;
    }
}
