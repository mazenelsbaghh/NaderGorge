using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIModelTests
{
    [Fact]
    public void Model_contains_all_ai_roots_and_concurrency_tokens()
    {
        using var db = TestAppDbContextFactory.Create();
        var expected = new[]
        {
            typeof(LiveSupportAIPolicyVersion), typeof(LiveSupportAIKnowledgeEntry), typeof(LiveSupportAIKnowledgeRevision),
            typeof(LiveSupportAIPolicyKnowledgeRevision), typeof(LiveSupportAIConversationState), typeof(LiveSupportAITurn),
            typeof(LiveSupportAIPendingAction), typeof(LiveSupportAIVerificationPolicyQuestion),
            typeof(LiveSupportAIVerificationSession), typeof(LiveSupportAIVerificationAttempt)
        };

        Assert.All(expected, type => Assert.NotNull(db.Model.FindEntityType(type)));
        Assert.True(db.Model.FindEntityType(typeof(LiveSupportAIPolicyVersion))!.FindProperty("Version")!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(LiveSupportAIConversationState))!.FindProperty("Version")!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(LiveSupportAITurn))!.FindProperty("Version")!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(LiveSupportAIPendingAction))!.FindProperty("Version")!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(LiveSupportAIVerificationSession))!.FindProperty("Version")!.IsConcurrencyToken);
    }

    [Fact]
    public void Verification_attempt_has_no_raw_or_expected_answer_property()
    {
        using var db = TestAppDbContextFactory.Create();
        var names = db.Model.FindEntityType(typeof(LiveSupportAIVerificationAttempt))!.GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(names, name => name.Contains("Answer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Turn_source_message_and_action_idempotency_are_unique()
    {
        using var db = TestAppDbContextFactory.Create();
        var turn = db.Model.FindEntityType(typeof(LiveSupportAITurn))!;
        Assert.Contains(turn.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(LiveSupportAITurn.SourceMessageId));
        var action = db.Model.FindEntityType(typeof(LiveSupportAIPendingAction))!;
        Assert.Contains(action.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(LiveSupportAIPendingAction.IdempotencyKey));
    }
}
