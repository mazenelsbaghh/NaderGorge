using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAICompletionModelTests
{
    [Fact]
    public void Existing_enum_values_remain_stable_and_new_values_append()
    {
        Assert.Equal(0, (int)LiveSupportAIMode.AiActive);
        Assert.Equal(1, (int)LiveSupportAIMode.HumanQueued);
        Assert.Equal(2, (int)LiveSupportAIMode.HumanAssigned);
        Assert.Equal(3, (int)LiveSupportAIMode.AiResolved);
        Assert.Equal(5, (int)LiveSupportAITurnStatus.DiscardedAfterDisable);
        Assert.Equal(6, (int)LiveSupportAITurnStatus.ProviderCompleted);
        Assert.Equal(7, (int)LiveSupportAITurnStatus.Cancelled);
        Assert.Equal(5, (int)LiveSupportAIDecisionType.Handoff);
        Assert.Equal(7, (int)LiveSupportAIPendingActionStatus.Failed);
    }

    [Fact]
    public void Pending_decision_supports_non_action_flows_without_fake_user()
    {
        var pending = new LiveSupportAIPendingAction
        {
            DecisionKind = LiveSupportAIPendingDecisionKind.Handoff,
            StudentUserId = null
        };

        Assert.Null(pending.StudentUserId);
        Assert.Equal(LiveSupportAIPendingDecisionKind.Handoff, pending.DecisionKind);
    }

    [Fact]
    public void Completion_model_contains_callback_recovery_and_verification_cursor_fields()
    {
        using var db = TestAppDbContextFactory.Create();
        var turn = db.Model.FindEntityType(typeof(LiveSupportAITurn))!;
        Assert.NotNull(turn.FindProperty(nameof(LiveSupportAITurn.CallbackStatus)));
        Assert.NotNull(turn.FindProperty(nameof(LiveSupportAITurn.NextCallbackAttemptAt)));
        Assert.NotNull(turn.FindProperty(nameof(LiveSupportAITurn.DecisionHash)));

        var state = db.Model.FindEntityType(typeof(LiveSupportAIConversationState))!;
        Assert.NotNull(state.FindProperty(nameof(LiveSupportAIConversationState.LastEventSequence)));
        Assert.NotNull(state.FindProperty(nameof(LiveSupportAIConversationState.DisableRequestedAt)));

        var verification = db.Model.FindEntityType(typeof(LiveSupportAIVerificationSession))!;
        Assert.NotNull(verification.FindProperty(nameof(LiveSupportAIVerificationSession.CurrentQuestionIndex)));
        Assert.NotNull(verification.FindProperty(nameof(LiveSupportAIVerificationSession.LockedAt)));
    }

    [Fact]
    public void Verification_attempt_still_has_no_raw_answer_storage()
    {
        var properties = typeof(LiveSupportAIVerificationAttempt).GetProperties();
        Assert.DoesNotContain(properties, property =>
            property.Name.Contains("Answer", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("LookupValue", StringComparison.OrdinalIgnoreCase));
    }
}
