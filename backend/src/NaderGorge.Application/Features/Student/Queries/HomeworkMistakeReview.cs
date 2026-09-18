using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities.Homework;

namespace NaderGorge.Application.Features.Student.Queries;

public record HomeworkMistakeGroupDto(Guid SubmissionId, Guid HomeworkId, string HomeworkTitle,
    Guid LessonId, Guid? PackageId, decimal Score, decimal TotalScore, List<HomeworkMistakeItemDto> Items);
public record HomeworkMistakeItemDto(Guid QuestionId, int Order, string QuestionText,
    string YourAnswer, string? CorrectAnswer, int ScoreReceived, int MaxPoints, string? ImageUrl);

public static class HomeworkMistakeReview
{
    public static HomeworkMistakeGroupDto Create(HomeworkSubmission submission, Guid? packageId)
    {
        // The graded attempt's definition survives later edits to the live homework.
        var homework = AssessmentDefinitionSnapshot.ResolveHomework(submission.Homework, submission.DefinitionSnapshotJson);
        var questions = homework.Questions.ToDictionary(question => question.Id);
        var mistakes = submission.Answers.Where(answer => answer.ScoreReceived.HasValue
                && questions.TryGetValue(answer.QuestionId, out var question) && answer.ScoreReceived < question.PointsActive)
            .Select(answer => ToMistake(questions[answer.QuestionId], answer)).OrderBy(question => question.Order).ToList();
        return new(submission.Id, homework.Id, homework.Title, homework.LessonId,
            packageId, submission.OverallScore,
            submission.TotalScoreSnapshot ?? homework.TotalScore, mistakes);
    }

    private static HomeworkMistakeItemDto ToMistake(HomeworkQuestion question, HomeworkAnswer answer) =>
        new(question.Id, question.Order, question.BodyText, answer.ProvidedAnswer,
            CorrectAnswer(question), answer.ScoreReceived!.Value, question.PointsActive, question.ImageUrl);

    private static string? CorrectAnswer(HomeworkQuestion question)
    {
        if (question.QuestionType == QuestionType.MCQ) return question.CorrectAnswerKey;
        if (question.QuestionType == QuestionType.Essay) return question.WrittenCorrection;
        if (question.BaseText is { } text && question.MistakeStartIndex is >= 0
            && question.MistakeEndIndex is { } end && end <= text.Length && end > question.MistakeStartIndex)
            return text[question.MistakeStartIndex.Value..end];
        return null;
    }
}
