using NaderGorge.Application.Features.Assessments;
using Xunit;

namespace NaderGorge.Application.Tests;

public class AssessmentRevisionPolicyTests
{
    [Fact]
    public void PreserveAttemptsKeepsGradesAndQuestionScaleDespiteDefinitionChanges()
    {
        var previous = new RevisionAnswer(Guid.NewGuid(), 10, 7, true);
        var plan = AssessmentRevisionPlanner.Plan([previous], [new(previous.QuestionId, 2)],
            new Dictionary<Guid, decimal?>(), new());

        Assert.Equal(previous, Assert.Single(plan.Answers));
        Assert.Equal(10, plan.MaximumPoints);
        Assert.Equal(7, plan.AwardedPoints);
        Assert.False(plan.RequiresReview);
    }

    [Theory]
    [InlineData(RemovedQuestionPolicy.KeepPreviousGrade, 10, 4)]
    [InlineData(RemovedQuestionPolicy.Exclude, 0, 0)]
    [InlineData(RemovedQuestionPolicy.AwardFullPoints, 10, 10)]
    public void RemovedQuestionUsesChosenPolicyWithoutDeletingOriginalAnswer(
        RemovedQuestionPolicy policy, int maximum, int awarded)
    {
        var previous = new RevisionAnswer(Guid.NewGuid(), 10, 4, false);
        var plan = AssessmentRevisionPlanner.Plan([previous], [], new Dictionary<Guid, decimal?>(),
            new(PreviousAttemptsPolicy.Regrade, policy));

        Assert.Single(plan.Answers);
        Assert.Equal(maximum, plan.MaximumPoints);
        Assert.Equal(awarded, plan.AwardedPoints);
        Assert.Equal(4, previous.AwardedPoints);
        Assert.False(previous.Excluded);
    }

    [Theory]
    [InlineData(AddedQuestionPolicy.FutureAttemptsOnly, 0, false)]
    [InlineData(AddedQuestionPolicy.RequestCompletion, 8, true)]
    public void NewQuestionsOnlyRequireStudentWorkWhenRequested(AddedQuestionPolicy policy, int maximum, bool completion)
    {
        var plan = AssessmentRevisionPlanner.Plan([], [new(Guid.NewGuid(), 8)],
            new Dictionary<Guid, decimal?>(), new(PreviousAttemptsPolicy.Regrade, AddedQuestions: policy));

        Assert.Equal(maximum, plan.MaximumPoints);
        Assert.Equal(completion, plan.RequiresCompletion);
        Assert.False(plan.RequiresReview);
        Assert.Equal(0, plan.AwardedPoints);
    }

    [Theory]
    [InlineData(ManualGradePolicy.Preserve, 10, 7, false)]
    [InlineData(ManualGradePolicy.ReturnForReview, 2, 0, true)]
    public void ManualGradesAreNotSilentlyOverwrittenOrClamped(
        ManualGradePolicy policy, int maximum, int awarded, bool review)
    {
        var previous = new RevisionAnswer(Guid.NewGuid(), 10, 7, true);
        var plan = AssessmentRevisionPlanner.Plan([previous], [new(previous.QuestionId, 2)],
            new Dictionary<Guid, decimal?> { [previous.QuestionId] = 0 },
            new(PreviousAttemptsPolicy.Regrade, ManualGrades: policy));

        Assert.Equal(maximum, plan.MaximumPoints);
        Assert.Equal(awarded, plan.AwardedPoints);
        Assert.Equal(review, plan.RequiresReview);
        Assert.Equal(7, previous.AwardedPoints);
    }

    [Fact]
    public void RegradeUsesRevisedPointsAndSupportsPendingEssayReview()
    {
        var objective = new RevisionAnswer(Guid.NewGuid(), 10, 0, false);
        var essay = new RevisionAnswer(Guid.NewGuid(), 5, 3, false);
        var plan = AssessmentRevisionPlanner.Plan([objective, essay],
            [new(objective.QuestionId, 4), new(essay.QuestionId, 6)],
            new Dictionary<Guid, decimal?> { [objective.QuestionId] = 4, [essay.QuestionId] = null },
            new(PreviousAttemptsPolicy.Regrade));

        Assert.Equal(10, plan.MaximumPoints);
        Assert.Equal(4, plan.AwardedPoints);
        Assert.True(plan.RequiresReview);
        Assert.False(plan.RequiresCompletion);
    }

    [Fact]
    public void MissingRecalculationDoesNotSilentlyTurnAnAnswerIntoZero()
    {
        var previous = new RevisionAnswer(Guid.NewGuid(), 10, 8, false);
        Assert.Throws<ArgumentException>(() => AssessmentRevisionPlanner.Plan([previous],
            [new(previous.QuestionId, 10)], new Dictionary<Guid, decimal?>(), new(PreviousAttemptsPolicy.Regrade)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void InvalidRecalculatedGradesAreRejected(int grade)
    {
        var previous = new RevisionAnswer(Guid.NewGuid(), 10, 8, false);
        Assert.Throws<ArgumentOutOfRangeException>(() => AssessmentRevisionPlanner.Plan([previous],
            [new(previous.QuestionId, 10)], new Dictionary<Guid, decimal?> { [previous.QuestionId] = grade },
            new(PreviousAttemptsPolicy.Regrade)));
    }
}
