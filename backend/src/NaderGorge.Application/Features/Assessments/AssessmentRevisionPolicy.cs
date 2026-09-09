using NaderGorge.Application.Services;

namespace NaderGorge.Application.Features.Assessments;

public enum PreviousAttemptsPolicy { Preserve, Regrade }
public enum RemovedQuestionPolicy { KeepPreviousGrade, Exclude, AwardFullPoints }
public enum AddedQuestionPolicy { FutureAttemptsOnly, RequestCompletion }
public enum ManualGradePolicy { Preserve, ReturnForReview }
public enum ScoreDecreasePolicy { Allow, Prevent }

public sealed record AssessmentRevisionPolicy(
    PreviousAttemptsPolicy PreviousAttempts = PreviousAttemptsPolicy.Preserve,
    RemovedQuestionPolicy RemovedQuestions = RemovedQuestionPolicy.KeepPreviousGrade,
    AddedQuestionPolicy AddedQuestions = AddedQuestionPolicy.FutureAttemptsOnly,
    ManualGradePolicy ManualGrades = ManualGradePolicy.Preserve,
    ScoreDecreasePolicy ScoreDecrease = ScoreDecreasePolicy.Allow);

public sealed record RevisionQuestion(Guid Id, decimal MaximumPoints);

public sealed record RevisionAnswer(
    Guid QuestionId,
    decimal MaximumPoints,
    decimal? AwardedPoints,
    bool ManuallyGraded,
    bool RequiresCompletion = false,
    bool Excluded = false);

public sealed record AttemptRevisionPlan(IReadOnlyList<RevisionAnswer> Answers)
{
    public decimal MinimumScoreRatio { get; init; }
    public decimal MaximumPoints => Answers.Where(x => !x.Excluded).Sum(x => x.MaximumPoints);
    public decimal AwardedPoints => Answers.Where(x => !x.Excluded).Sum(x => x.AwardedPoints ?? 0);
    public bool RequiresCompletion => Answers.Any(x => !x.Excluded && x.RequiresCompletion);
    public bool RequiresReview => Answers.Any(x => !x.Excluded && !x.RequiresCompletion && x.AwardedPoints is null);

    public decimal ScaledScore(decimal totalScore) => Math.Max(
        GradingEvaluationService.CalculateScaledScore(AwardedPoints, MaximumPoints, totalScore),
        Math.Round(MinimumScoreRatio * totalScore, 2));
}

/// <summary>Reconciles grades without modifying the source attempt or deleting its answers.</summary>
public static class AssessmentRevisionPlanner
{
    public static AttemptRevisionPlan Plan(
        IReadOnlyList<RevisionAnswer> previousAnswers,
        IReadOnlyList<RevisionQuestion> revisedQuestions,
        IReadOnlyDictionary<Guid, decimal?> recalculatedGrades,
        AssessmentRevisionPolicy policy)
    {
        ValidatePolicy(policy);
        ValidateQuestions(previousAnswers, revisedQuestions);
        if (policy.PreviousAttempts == PreviousAttemptsPolicy.Preserve)
            return new AttemptRevisionPlan(previousAnswers.ToArray());

        var revisedById = revisedQuestions.ToDictionary(x => x.Id);
        var answers = previousAnswers.Select(previous => revisedById.TryGetValue(previous.QuestionId, out var revised)
            ? ReviseAnswer(previous, revised, recalculatedGrades, policy.ManualGrades)
            : RemoveQuestion(previous, policy.RemovedQuestions)).ToList();

        if (policy.AddedQuestions == AddedQuestionPolicy.RequestCompletion)
        {
            var previousIds = previousAnswers.Select(x => x.QuestionId).ToHashSet();
            answers.AddRange(revisedQuestions.Where(x => !previousIds.Contains(x.Id))
                .Select(x => new RevisionAnswer(x.Id, x.MaximumPoints, null, false, RequiresCompletion: true)));
        }

        return new AttemptRevisionPlan(answers);
    }

    private static RevisionAnswer ReviseAnswer(
        RevisionAnswer previous,
        RevisionQuestion revised,
        IReadOnlyDictionary<Guid, decimal?> recalculatedGrades,
        ManualGradePolicy manualPolicy)
    {
        if (previous.ManuallyGraded && manualPolicy == ManualGradePolicy.Preserve)
        {
            // Keeping a manual grade also keeps its original scale; silently clamping changes that grade.
            return previous with { Excluded = false };
        }

        if (previous.RequiresCompletion)
            return previous with { MaximumPoints = revised.MaximumPoints, Excluded = false };

        if (previous.ManuallyGraded)
            return previous with { MaximumPoints = revised.MaximumPoints, AwardedPoints = null, Excluded = false };

        if (!recalculatedGrades.TryGetValue(previous.QuestionId, out var grade))
            throw new ArgumentException("A recalculated grade is required for every retained automatic answer.", nameof(recalculatedGrades));
        if (grade < 0 || grade > revised.MaximumPoints)
            throw new ArgumentOutOfRangeException(nameof(recalculatedGrades), "A grade must be within the revised question's points.");

        return previous with { MaximumPoints = revised.MaximumPoints, AwardedPoints = grade, Excluded = false };
    }

    private static RevisionAnswer RemoveQuestion(RevisionAnswer previous, RemovedQuestionPolicy policy) => policy switch
    {
        RemovedQuestionPolicy.KeepPreviousGrade => previous,
        RemovedQuestionPolicy.Exclude => previous with { Excluded = true },
        RemovedQuestionPolicy.AwardFullPoints => previous with
        {
            AwardedPoints = previous.MaximumPoints, RequiresCompletion = false, Excluded = false
        },
        _ => throw new ArgumentOutOfRangeException(nameof(policy))
    };

    private static void ValidatePolicy(AssessmentRevisionPolicy policy)
    {
        if (!Enum.IsDefined(policy.PreviousAttempts) || !Enum.IsDefined(policy.RemovedQuestions)
            || !Enum.IsDefined(policy.AddedQuestions) || !Enum.IsDefined(policy.ManualGrades)
            || !Enum.IsDefined(policy.ScoreDecrease))
            throw new ArgumentException("Unknown assessment revision policy.", nameof(policy));
    }

    private static void ValidateQuestions(IReadOnlyList<RevisionAnswer> previous, IReadOnlyList<RevisionQuestion> revised)
    {
        if (previous.Select(x => x.QuestionId).Distinct().Count() != previous.Count
            || revised.Select(x => x.Id).Distinct().Count() != revised.Count)
            throw new ArgumentException("Each question must occur once in an attempt definition.");
        if (previous.Any(x => x.QuestionId == Guid.Empty || x.MaximumPoints < 0
                || x.AwardedPoints < 0 || x.AwardedPoints > x.MaximumPoints)
            || revised.Any(x => x.Id == Guid.Empty || x.MaximumPoints < 0))
            throw new ArgumentException("Question identities and grade ranges must be valid.");
    }
}
