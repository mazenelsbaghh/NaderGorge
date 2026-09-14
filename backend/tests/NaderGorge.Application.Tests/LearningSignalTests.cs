using NaderGorge.Application.Features.LearningCenter;

namespace NaderGorge.Application.Tests;

public sealed class LearningSignalTests
{
    [Theory]
    [InlineData("same", 20, true)]
    [InlineData("different", 20, false)]
    [InlineData("same", 65, false)]
    public void Decline_requires_comparable_definitions_and_the_configured_drop(string latestDefinition, decimal latestScore, bool expectedAlert)
    {
        var examId = Guid.NewGuid();
        LearningAttemptEvidence[] attempts =
        [
            Attempt(examId, "same", 70, -2),
            Attempt(examId, latestDefinition, latestScore, -1)
        ];
        var reasons = LearningFollowUps.Reasons(attempts, new(DeclinePoints: 15));
        Assert.Equal(expectedAlert, reasons.Count > 0);
    }

    [Theory]
    [InlineData(35, true)]
    [InlineData(45, false)]
    [InlineData(65, false)]
    public void Repeated_attempt_alert_requires_persistently_low_scores_without_improvement(decimal lastScore, bool expectedAlert)
    {
        var examId = Guid.NewGuid();
        LearningAttemptEvidence[] attempts =
        [
            Attempt(examId, "same", 40, -3),
            Attempt(examId, "same", 35, -2),
            Attempt(examId, "same", lastScore, -1)
        ];
        Assert.Equal(expectedAlert, LearningFollowUps.Reasons(attempts, new()).Count > 0);
    }

    private static LearningAttemptEvidence Attempt(Guid examId, string definition, decimal percent, int day) =>
        new(Guid.NewGuid(), Guid.Empty, "طالب", examId, Guid.Empty, DateTime.UtcNow.AddDays(day), percent, definition, []);
}
