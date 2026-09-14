using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Assessments;

public sealed record RecordedAssessmentAnswer(Guid QuestionId, string? Text, Guid? SelectedOptionId,
    decimal? AwardedPoints, bool ManuallyGraded);
public sealed record AssessmentDefinitionChange(AssessmentDefinitionSnapshot Published,
    AssessmentDefinitionSnapshot Proposed, AssessmentRevisionPolicy Policy)
{
    public decimal? PreviousScore { get; init; }
}
public sealed record AssessmentAttemptRevision(AssessmentDefinitionSnapshot Definition, AttemptRevisionPlan Grades);

public static class AssessmentAttemptRegrader
{
    public static AssessmentAttemptRevision Regrade(AssessmentDefinitionSnapshot previous,
        IReadOnlyList<RecordedAssessmentAnswer> recorded, AssessmentDefinitionChange change)
    {
        if (previous.Kind != change.Proposed.Kind || previous.AssessmentId != change.Proposed.AssessmentId
            || change.Published.Kind != previous.Kind || change.Published.AssessmentId != previous.AssessmentId)
            throw new ArgumentException("تعريف التعديل لا يخص هذه المحاولة.");
        var answers = recorded.ToDictionary(a => a.QuestionId);
        var oldGrades = PreviousGrades(previous, answers);
        var assignedIds = previous.Questions.Select(q => q.Id).ToHashSet();
        var publishedIds = change.Published.Questions.Select(q => q.Id).ToHashSet();
        var revised = change.Proposed.Questions.Where(q => assignedIds.Contains(q.Id) || !publishedIds.Contains(q.Id)).ToArray();
        var recalculated = revised.Where(q => answers.ContainsKey(q.Id)).ToDictionary(q => q.Id,
            q => AutomaticGrade(q, answers[q.Id], change.Proposed.Kind));
        foreach (var question in revised.Where(q => assignedIds.Contains(q.Id) && !recalculated.ContainsKey(q.Id)))
            recalculated[question.Id] = question.Type == (int)QuestionType.Essay ? null : 0;
        var grades = AssessmentRevisionPlanner.Plan(oldGrades, revised.Select(q => new RevisionQuestion(q.Id, q.Points)).ToArray(),
            recalculated, change.Policy);
        if (change.Policy.PreviousAttempts == PreviousAttemptsPolicy.Preserve) return new(previous, grades);
        if (change.Policy.ScoreDecrease == ScoreDecreasePolicy.Prevent)
        {
            var previousScore = change.PreviousScore ?? (previous.Revision ?? new AttemptRevisionPlan(oldGrades)).ScaledScore(previous.TotalScore);
            grades = grades with { MinimumScoreRatio = previous.TotalScore > 0 ? Math.Clamp(previousScore / previous.TotalScore, 0, 1) : 0 };
        }
        var definition = RevisedDefinition(previous, change.Proposed, grades, change.Policy);
        return new(definition, grades);
    }

    private static RevisionAnswer[] PreviousGrades(AssessmentDefinitionSnapshot previous,
        IReadOnlyDictionary<Guid, RecordedAssessmentAnswer> answers) => previous.Questions.Select(q =>
    {
        answers.TryGetValue(q.Id, out var answer);
        var saved = previous.Revision?.Answers.SingleOrDefault(a => a.QuestionId == q.Id);
        return new RevisionAnswer(q.Id, saved?.MaximumPoints ?? q.Points,
            saved?.Excluded == true ? saved.AwardedPoints : answer?.AwardedPoints,
            saved?.ManuallyGraded ?? answer?.ManuallyGraded ?? false,
            saved?.RequiresCompletion ?? false, saved?.Excluded ?? false);
    }).ToArray();

    private static AssessmentDefinitionSnapshot RevisedDefinition(AssessmentDefinitionSnapshot previous,
        AssessmentDefinitionSnapshot proposed, AttemptRevisionPlan grades, AssessmentRevisionPolicy policy)
    {
        var oldQuestions = previous.Questions.ToDictionary(q => q.Id);
        var proposedQuestions = proposed.Questions.ToDictionary(q => q.Id);
        var questions = grades.Answers.Select(grade =>
        {
            var keepDefinition = !proposedQuestions.ContainsKey(grade.QuestionId)
                || (grade.ManuallyGraded && policy.ManualGrades == ManualGradePolicy.Preserve);
            var question = keepDefinition ? oldQuestions[grade.QuestionId] : proposedQuestions[grade.QuestionId];
            return question with
            {
                Points = grade.Excluded ? 0 : grade.MaximumPoints,
                CorrectAnswerKey = proposed.Kind == "homework" && question.Type == (int)QuestionType.MCQ
                    ? question.Options.FirstOrDefault(o => o.IsCorrect)?.Text ?? question.CorrectAnswerKey
                    : question.CorrectAnswerKey
            };
        }).OrderBy(q => q.Order).ThenBy(q => q.Id).ToArray();
        var assignedIds = questions.Select(q => q.Id).ToHashSet();
        return proposed with
        {
            Questions = questions, Revision = grades,
            ReserveQuestions = proposed.Questions.Where(q => !assignedIds.Contains(q.Id)).ToArray()
        };
    }

    internal static decimal? AutomaticGrade(AssessmentQuestionSnapshot question, RecordedAssessmentAnswer answer, string kind)
    {
        if (question.Type == (int)QuestionType.Essay) return null;
        var correct = question.Type == (int)QuestionType.FindTheMistake
            ? CorrectMistake(question, answer.Text)
            : kind == "homework"
                ? question.Options.Any(o => o.IsCorrect && string.Equals(answer.Text?.Trim(), o.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                : question.Options.Any(o => o.Id == answer.SelectedOptionId && o.IsCorrect);
        if (!correct) return 0;
        return question.Points;
    }

    private static bool CorrectMistake(AssessmentQuestionSnapshot question, string? answer)
    {
        if (question.BaseText is null || question.MistakeStartIndex is not int start || question.MistakeEndIndex is not int end
            || start < 0 || end <= start || end > question.BaseText.Length)
            throw new ArgumentException("موضع الخطأ غير صالح لإعادة التصحيح.");
        return string.Equals(answer?.Trim(), question.BaseText[start..end].Trim(), StringComparison.Ordinal);
    }
}
