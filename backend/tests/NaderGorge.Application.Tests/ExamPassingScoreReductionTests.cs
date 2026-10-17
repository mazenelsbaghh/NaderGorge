using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public class ExamPassingScoreReductionTests
{
    // 2026-10-17: 60.75/70 stayed failed after the pass threshold dropped to 35.
    [Theory]
    [InlineData(60.75, 70, "ضعيف", false, true, "جيد جداً")]
    [InlineData(35, 70, "ضعيف", false, true, "مقبول")]
    [InlineData(34.99, 70, "ضعيف", false, false, "ضعيف")]
    [InlineData(60.75, 70, "قيد التصحيح", false, false, "قيد التصحيح")]
    [InlineData(60.75, 70, null, false, false, null)]
    [InlineData(0, 70, "انتهى الوقت", true, false, "انتهى الوقت")]
    [InlineData(12, 14, "ضعيف", false, true, "جيد جداً")]
    public void LowerThresholdPreservesGradesAndOnlyFinalizesEligibleAttempts(decimal score, decimal total,
        string? evaluation, bool expired, bool expectedPass, string? expectedEvaluation)
    {
        using var db = TestAppDbContextFactory.Create();
        var exam = new Exam { Id = Guid.NewGuid(), Title = "Threshold", TotalScore = total, PassingScore = total };
        var saved = AssessmentDefinitionSnapshot.FromExam(exam);
        var answer = new StudentAnswer { Id = Guid.NewGuid(), SubmittedText = "Original answer", PointsAwarded = score };
        var attempt = new StudentExamAttempt { Id = Guid.NewGuid(), ExamId = exam.Id, ScoreAchieved = score,
            Evaluation = evaluation, IsTimeExpired = expired, DefinitionSnapshotJson = saved.ToJson(), Answers = [answer] };
        var request = Request(exam, saved with { TotalScore = 70, PassingScore = 35 });
        var reduction = new ExamPassingScoreReduction(db);

        reduction.Apply(attempt, [], request);
        reduction.Apply(attempt, [], request);

        var updated = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", exam.Id);
        Assert.Equal(total / 2, updated.PassingScore);
        Assert.Equal((saved with { PassingScore = updated.PassingScore }).ToJson(), updated.ToJson());
        Assert.Equal(score, attempt.ScoreAchieved);
        Assert.Equal(expectedPass, attempt.IsPassed);
        Assert.Equal(expectedEvaluation, attempt.Evaluation);
        Assert.Same(answer, Assert.Single(attempt.Answers));
        Assert.Equal(score, answer.PointsAwarded);
        Assert.Equal("Original answer", answer.SubmittedText);
        Assert.Single(db.AuditLogs.Local);
        Assert.Equal(expectedPass ? 1 : 0, db.OutboxEvents.Local.Count);
    }

    [Theory]
    [InlineData(35)]
    [InlineData(70)]
    public void ExistingLowerThresholdCannotBeRaisedByPreservedDefinitionEdits(decimal proposedThreshold)
    {
        using var db = TestAppDbContextFactory.Create();
        var exam = new Exam { Id = Guid.NewGuid(), TotalScore = 70, PassingScore = 20 };
        var saved = AssessmentDefinitionSnapshot.FromExam(exam);
        var attempt = new StudentExamAttempt { ExamId = exam.Id, ScoreAchieved = 25, IsPassed = true,
            Evaluation = "مقبول", DefinitionSnapshotJson = saved.ToJson() };

        new ExamPassingScoreReduction(db).Apply(attempt, [], Request(exam, saved with { PassingScore = proposedThreshold }));

        Assert.Equal(saved.ToJson(), attempt.DefinitionSnapshotJson);
        Assert.True(attempt.IsPassed);
        Assert.Empty(db.AuditLogs.Local);
    }

    [Theory]
    [InlineData("essay")]
    [InlineData("review")]
    [InlineData("completion")]
    public void LowerThresholdDoesNotBypassUnfinishedGradingOrRequiredQuestions(string unfinished)
    {
        using var db = TestAppDbContextFactory.Create();
        var exam = new Exam { Id = Guid.NewGuid(), TotalScore = 70, PassingScore = 70 };
        var saved = AssessmentDefinitionSnapshot.FromExam(exam);
        if (unfinished != "essay") saved = saved with
        { Revision = new([new(Guid.NewGuid(), 1, null, false, RequiresCompletion: unfinished == "completion")]) };
        var attempt = new StudentExamAttempt { Id = Guid.NewGuid(), ExamId = exam.Id, ScoreAchieved = 60.75m,
            Evaluation = "ضعيف", DefinitionSnapshotJson = saved.ToJson() };
        EssaySubmission[] essays = unfinished == "essay" ? [new() { Status = EssaySubmissionStatus.WaitTeacher }] : [];

        new ExamPassingScoreReduction(db).Apply(attempt, essays, Request(exam, saved with { PassingScore = 35 }));

        Assert.False(attempt.IsPassed);
        Assert.Equal("ضعيف", attempt.Evaluation);
        Assert.Equal(35, AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", exam.Id).PassingScore);
        Assert.Empty(db.OutboxEvents.Local);
    }

    private static SaveAssessmentRevisionCommand Request(Exam exam, AssessmentDefinitionSnapshot definition) =>
        new(new(AssessmentKind.Exam, exam.Id, Guid.Empty, Guid.NewGuid()), definition, new(), "preview", Guid.NewGuid());
}
