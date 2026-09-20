using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Assessments;

public sealed record AssessmentAttemptScaleProjection(
    AssessmentDefinitionSnapshot Definition, decimal ScoreAchieved, bool IsPassed);

public static class AssessmentAttemptScaleNormalizer
{
    public const string UnsupportedLegacyMessage =
        "لا يمكن اعتماد نتيجة هذه المحاولة القديمة لأن سلم الدرجات التاريخي غير محفوظ. تحتاج إلى تسوية يدوية من الدعم.";

    public static AssessmentAttemptScaleProjection Project(StudentExamAttempt attempt) => Project(
        attempt.DefinitionSnapshotJson, attempt.ExamId, attempt.ScoreAchieved, attempt.IsPassed,
        attempt.IsTimeExpired, attempt.Answers.Sum(answer => answer.PointsAwarded));

    public static AssessmentAttemptScaleProjection Project(
        string? snapshotJson, Guid examId, decimal scoreAchieved, bool isPassed,
        bool isTimeExpired, decimal awardedPoints)
    {
        if (snapshotJson is null)
            throw new InvalidOperationException(UnsupportedLegacyMessage);

        var previous = AssessmentDefinitionSnapshot.Read(snapshotJson, "exam", examId);
        if (previous.UsesAssignedQuestionPoints || previous.ReserveQuestions.Length == 0)
            return new(previous, scoreAchieved, isPassed);

        var normalized = previous.WithAssignedQuestionPoints();
        var protectedRatio = previous.Revision?.MinimumScoreRatio ?? 0;
        var protectedScore = Math.Round(protectedRatio * normalized.TotalScore, 2);
        var normalizedScore = isTimeExpired ? 0 : Math.Max(awardedPoints, protectedScore);
        return new(normalized, normalizedScore, isPassed);
    }

    public static bool Normalize(StudentExamAttempt attempt)
    {
        var projection = Project(attempt);
        var normalizedJson = projection.Definition.ToJson();
        if (normalizedJson == attempt.DefinitionSnapshotJson
            && projection.ScoreAchieved == attempt.ScoreAchieved && projection.IsPassed == attempt.IsPassed)
            return false;
        attempt.DefinitionSnapshotJson = normalizedJson;
        attempt.ScoreAchieved = projection.ScoreAchieved;
        attempt.IsPassed = projection.IsPassed;
        return true;
    }
}
