using NaderGorge.Domain.Entities;
using NaderGorge.Application.Features.Assessments;

namespace NaderGorge.Application.Features.Exams.Commands;

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
    string? StudentAudioUrl = null
);

public record QuestionReviewSnapshot(
    string? SelectedText,
    bool IsAnswered,
    bool IsCorrect,
    decimal PointsAwarded,
    string? StudentAudioUrl = null
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
        exam = AssessmentDefinitionSnapshot.ResolveExam(exam, attempt.DefinitionSnapshotJson);
        var questionReviews = exam.ExamQuestions
            .OrderBy(q => q.Order)
            .Select(eq =>
            {
                questionSnapshotsByQuestionId.TryGetValue(eq.Id, out var snapshot);
                var resultIsCompleted = string.Equals(resultState ?? "Completed", "Completed", StringComparison.Ordinal);
                var canRevealCorrectAnswer = revealCorrectAnswers
                    && (resultIsCompleted || eq.Question.Type != QuestionType.Essay);
                var correctText = GetCorrectReviewText(eq.Question);

                return new ExamQuestionReviewDto(
                    eq.Id,
                    eq.Order,
                    eq.Question.Text,
                    snapshot?.SelectedText,
                    snapshot?.IsAnswered ?? false,
                    snapshot?.IsCorrect ?? false,
                    snapshot?.PointsAwarded ?? 0,
                    canRevealCorrectAnswer ? correctText : null,
                    canRevealCorrectAnswer ? eq.Question.AudioUrl : null,
                    eq.Question.ImageUrl,
                    resultIsCompleted
                        ? eq.Question.WrittenCorrection
                        : null,
                    snapshot?.StudentAudioUrl
                );
            })
            .ToList();

        return new ExamResultDto(
            attempt.Id,
            attempt.ScoreAchieved,
            exam.TotalScore,
            attempt.IsPassed,
            blocksNextLesson,
            attempt.Evaluation ?? "غير مقيم",
            attempt.IsTimeExpired,
            resultState ?? "Completed",
            lessonId,
            packageId,
            questionReviews
        );
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
