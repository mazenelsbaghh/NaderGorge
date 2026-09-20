using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public class AssessmentAttemptScaleNormalizerTests
{
    [Fact]
    public void LegacyAttemptNormalizesSnapshotAndScoreFromSavedEvidenceExactlyOnce()
    {
        var (exam, assigned, reserve) = ExamWithWeightedReserve();
        var attempt = Attempt(exam, assigned, score: 17.33m, awarded: 13m);
        attempt.DefinitionSnapshotJson = LegacySnapshot(exam, assigned).ToJson();

        Assert.True(AssessmentAttemptScaleNormalizer.Normalize(attempt));
        var normalized = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", exam.Id);

        Assert.True(normalized.UsesAssignedQuestionPoints);
        Assert.Equal(15m, normalized.TotalScore);
        Assert.Equal(9m, normalized.PassingScore);
        Assert.Equal(13m, attempt.ScoreAchieved);
        Assert.Contains(reserve.Id, normalized.ReserveQuestions.Select(question => question.Id));
        Assert.False(AssessmentAttemptScaleNormalizer.Normalize(attempt));
        Assert.Equal(13m, attempt.ScoreAchieved);
    }

    [Fact]
    public void PreventDecreaseFloorIsConvertedFromOriginalScaleBeforeRegrade()
    {
        var (exam, assigned, _) = ExamWithWeightedReserve();
        var attempt = Attempt(exam, assigned, score: 16m, awarded: 10m);
        attempt.DefinitionSnapshotJson = (LegacySnapshot(exam, assigned) with
        {
            Revision = new AttemptRevisionPlan([]) { MinimumScoreRatio = 0.8m }
        }).ToJson();

        AssessmentAttemptScaleNormalizer.Normalize(attempt);

        Assert.Equal(12m, attempt.ScoreAchieved);
        Assert.Equal(15m, AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", exam.Id).TotalScore);
    }

    [Fact]
    public void SavedManualAwardRemainsAuthoritativeDuringNormalization()
    {
        var (exam, assigned, _) = ExamWithWeightedReserve();
        var attempt = Attempt(exam, assigned, score: 14m, awarded: 14m);
        attempt.DefinitionSnapshotJson = LegacySnapshot(exam, assigned).ToJson();

        AssessmentAttemptScaleNormalizer.Normalize(attempt);

        Assert.Equal(14m, attempt.ScoreAchieved);
    }

    [Fact]
    public void CompatibilityProjectionPreservesPersistedPassDecisionWhenCorrectedScoreIsBelowThreshold()
    {
        var (exam, assigned, _) = ExamWithWeightedReserve();
        var attempt = Attempt(exam, assigned, score: 16m, awarded: 1m);
        attempt.IsPassed = true;
        attempt.DefinitionSnapshotJson = LegacySnapshot(exam, assigned).ToJson();

        var projection = AssessmentAttemptScaleNormalizer.Project(attempt);

        Assert.Equal(1m, projection.ScoreAchieved);
        Assert.Equal(15m, projection.Definition.TotalScore);
        Assert.Equal(9m, projection.Definition.PassingScore);
        Assert.True(projection.IsPassed);
    }

    [Fact]
    public void NullSnapshotAfterExamEditsRequiresManualReconciliationWithoutGuessing()
    {
        var (exam, _, _) = ExamWithWeightedReserve();
        var attempt = new StudentExamAttempt { ExamId = exam.Id, DefinitionSnapshotJson = null, ScoreAchieved = 17.33m };
        exam.TotalScore = 40m;
        exam.PassingScore = 30m;
        foreach (var question in exam.ExamQuestions) question.Points *= 2;

        var error = Assert.Throws<InvalidOperationException>(() =>
            AssessmentAttemptScaleNormalizer.Normalize(attempt));

        Assert.Equal(AssessmentAttemptScaleNormalizer.UnsupportedLegacyMessage, error.Message);
        Assert.Null(attempt.DefinitionSnapshotJson);
        Assert.Equal(17.33m, attempt.ScoreAchieved);
    }

    private static StudentExamAttempt Attempt(Exam exam, ExamQuestion[] assigned, decimal score, decimal awarded)
    {
        var attempt = new StudentExamAttempt { ExamId = exam.Id, ScoreAchieved = score };
        attempt.Answers = assigned.Select((question, index) => new StudentAnswer
        {
            ExamQuestionId = question.Id,
            PointsAwarded = index == 0 ? awarded : 0
        }).ToList();
        return attempt;
    }

    private static AssessmentDefinitionSnapshot LegacySnapshot(Exam exam, ExamQuestion[] assigned) =>
        AssessmentDefinitionSnapshot.FromExam(exam, assigned) with
        { UsesAssignedQuestionPoints = false, TotalScore = exam.TotalScore, PassingScore = exam.PassingScore };

    private static (Exam Exam, ExamQuestion[] Assigned, ExamQuestion Reserve) ExamWithWeightedReserve()
    {
        var first = Question(10m);
        var second = Question(5m);
        var reserve = Question(5m);
        var exam = new Exam
        {
            Id = Guid.NewGuid(), Title = "Weighted", TotalScore = 20m, PassingScore = 12m,
            IsRandomized = true, DisplayQuestionCount = 2, ExamQuestions = [first, second, reserve]
        };
        foreach (var question in exam.ExamQuestions) question.ExamId = exam.Id;
        return (exam, [first, second], reserve);
    }

    private static ExamQuestion Question(decimal points) => new()
    {
        Id = Guid.NewGuid(), Points = points, Question = new QuestionBankItem
        {
            Id = Guid.NewGuid(), Text = "Question", Type = QuestionType.MCQ,
            Options = [new() { Id = Guid.NewGuid(), Text = "Correct", IsCorrect = true }]
        }
    };
}
