using NaderGorge.Application.Features.Assessments;

namespace NaderGorge.Application.Tests;

public class AssessmentAttemptRegraderTests
{
    [Theory]
    [InlineData(ScoreDecreasePolicy.Prevent, 40, 30)]
    [InlineData(ScoreDecreasePolicy.Prevent, 10, 7.5)]
    [InlineData(ScoreDecreasePolicy.Allow, 40, 0)]
    public void ChangedRubricHonorsChosenScoreFloorOnNewScale(ScoreDecreasePolicy policy, decimal total, decimal expected)
    {
        var question = Question();
        var previous = Definition([question]);
        var proposed = previous with { TotalScore = total,
            Questions = [question with { Options = question.Options.Select(o => o with { IsCorrect = o.Text == "B" }).ToArray() }] };
        var revision = AssessmentAttemptRegrader.Regrade(previous,
            [new(question.Id, "A", question.Options[0].Id, 3, false)],
            new(previous, proposed, new(PreviousAttemptsPolicy.Regrade, ScoreDecrease: policy)) { PreviousScore = 15 });

        Assert.Equal(0, revision.Grades.AwardedPoints);
        Assert.Equal(expected, revision.Grades.ScaledScore(total));
        var saved = AssessmentDefinitionSnapshot.Read(revision.Definition.ToJson(), "exam", previous.AssessmentId);
        var manuallyGraded = saved.WithGrades(new Dictionary<Guid, (decimal, bool)> { [question.Id] = (0, true) });
        Assert.Equal(expected, manuallyGraded.Revision!.ScaledScore(total));
    }

    [Fact]
    public void PreventingDecreaseDoesNotCapAnImprovedGrade()
    {
        var question = Question();
        var previous = Definition([question]);
        var revision = AssessmentAttemptRegrader.Regrade(previous,
            [new(question.Id, "A", question.Options[0].Id, 0, false)],
            new(previous, previous, new(PreviousAttemptsPolicy.Regrade, ScoreDecrease: ScoreDecreasePolicy.Prevent)) { PreviousScore = 5 });
        Assert.Equal(20, revision.Grades.ScaledScore(20));
    }

    [Fact]
    public void ChangedCorrectOptionRegradesSelectedIdentityAndPreserveKeepsOriginalDefinition()
    {
        var question = Question();
        var previous = Definition([question]);
        var selected = question.Options[1].Id;
        var proposed = previous with
        {
            TotalScore = 40,
            Questions = [question with { Options = question.Options.Select(o => o with { IsCorrect = o.Id == selected }).ToArray() }]
        };
        RecordedAssessmentAnswer[] answers = [new(question.Id, "B", selected, 0, false)];

        var regraded = AssessmentAttemptRegrader.Regrade(previous, answers,
            new(previous, proposed, new(PreviousAttemptsPolicy.Regrade)));
        Assert.Equal(4, regraded.Grades.AwardedPoints);
        Assert.Equal(40, regraded.Definition.TotalScore);
        Assert.Equal(0, answers[0].AwardedPoints);
        var preserved = AssessmentAttemptRegrader.Regrade(previous, answers, new(previous, proposed, new()));
        Assert.Same(previous, preserved.Definition);
        Assert.Equal(0, preserved.Grades.AwardedPoints);
    }

    [Theory]
    [InlineData(RemovedQuestionPolicy.KeepPreviousGrade, 4, 2)]
    [InlineData(RemovedQuestionPolicy.Exclude, 0, 0)]
    [InlineData(RemovedQuestionPolicy.AwardFullPoints, 4, 4)]
    public void RemovedQuestionRetainsHistoryAndAppliesChosenGradePolicy(RemovedQuestionPolicy policy, decimal maximum, decimal awarded)
    {
        var question = Question();
        var previous = Definition([question]);
        var proposed = previous with { Questions = [] };
        var revision = AssessmentAttemptRegrader.Regrade(previous, [new(question.Id, "A", question.Options[0].Id, 2, false)],
            new(previous, proposed, new(PreviousAttemptsPolicy.Regrade, policy)));

        Assert.Equal(maximum, revision.Grades.MaximumPoints);
        Assert.Equal(awarded, revision.Grades.AwardedPoints);
        Assert.Equal(question.Id, Assert.Single(revision.Definition.Questions).Id);
        Assert.Equal(question.Text, revision.Definition.Questions[0].Text);
        Assert.Equal(maximum, revision.Definition.Questions[0].Points);
        Assert.Equal(question.Options, revision.Definition.Questions[0].Options);
    }

    [Fact]
    public void AddedCompletionDoesNotAssignPreviouslyUnselectedRandomQuestions()
    {
        var assigned = Question();
        var unassigned = Question();
        var added = Question();
        var published = Definition([assigned, unassigned]);
        var previous = published with { Questions = [assigned], ReserveQuestions = [unassigned] };
        var proposed = published with { Questions = [assigned, unassigned, added] };
        var revision = AssessmentAttemptRegrader.Regrade(previous,
            [new(assigned.Id, "A", assigned.Options[0].Id, 4, false)],
            new(published, proposed, new(PreviousAttemptsPolicy.Regrade, AddedQuestions: AddedQuestionPolicy.RequestCompletion)));

        Assert.Equal(added.Id, Assert.Single(revision.Grades.Answers, a => a.RequiresCompletion).QuestionId);
        Assert.DoesNotContain(revision.Grades.Answers, a => a.QuestionId == unassigned.Id);
        Assert.Equal(unassigned.Id, Assert.Single(revision.Definition.ReserveQuestions).Id);
        Assert.Equal(8, revision.Grades.MaximumPoints);
        Assert.Equal(4, revision.Grades.AwardedPoints);
    }

    [Theory]
    [InlineData(ManualGradePolicy.Preserve, false, 4)]
    [InlineData(ManualGradePolicy.ReturnForReview, true, 1)]
    public void ManualGradeChoiceControlsOriginalScaleAndReviewState(ManualGradePolicy policy, bool requiresReview, decimal maximum)
    {
        var question = Question();
        var previous = Definition([question]);
        var proposed = previous with { Questions = [question with { Text = "Revised", Points = 1 }] };
        var revision = AssessmentAttemptRegrader.Regrade(previous, [new(question.Id, "B", question.Options[1].Id, 3, true)],
            new(previous, proposed, new(PreviousAttemptsPolicy.Regrade, ManualGrades: policy)));

        Assert.Equal(requiresReview, revision.Grades.RequiresReview);
        Assert.Equal(maximum, revision.Grades.MaximumPoints);
        Assert.Equal(requiresReview ? null : (decimal?)3, Assert.Single(revision.Grades.Answers).AwardedPoints);
        Assert.Equal(requiresReview ? "Revised" : question.Text, Assert.Single(revision.Definition.Questions).Text);
    }

    [Fact]
    public void HomeworkRegradeUsesRevisedCorrectChoiceInsteadOfStaleAnswerKey()
    {
        var question = Question() with { CorrectAnswerKey = "A" };
        var previous = Definition([question]) with { Kind = "homework" };
        var proposed = previous with
        { Questions = [question with { Options = question.Options.Select(o => o with { IsCorrect = o.Text == "B" }).ToArray() }] };
        var revision = AssessmentAttemptRegrader.Regrade(previous, [new(question.Id, " b ", null, 0, false)],
            new(previous, proposed, new(PreviousAttemptsPolicy.Regrade)));
        Assert.Equal(4, revision.Grades.AwardedPoints);
        Assert.Equal("B", Assert.Single(revision.Definition.Questions).CorrectAnswerKey);
    }

    private static AssessmentDefinitionSnapshot Definition(AssessmentQuestionSnapshot[] questions) =>
        new(1, "exam", Guid.NewGuid(), "Exam", null, 20, 10, 30, true, false, null, questions);

    private static AssessmentQuestionSnapshot Question() => new(Guid.NewGuid(), Guid.NewGuid(), 1, 0,
        "Original", 4, null, null, null, null, null, null, null,
        [new(Guid.NewGuid(), "A", true), new(Guid.NewGuid(), "B", false)], null);
}
