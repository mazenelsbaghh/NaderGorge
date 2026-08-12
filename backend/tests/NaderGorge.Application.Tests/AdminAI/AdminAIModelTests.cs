using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIModelTests
{
    [Fact]
    public void NewConversationAndTurn_StartWithPositiveVersionsAndBoundedCounters()
    {
        var conversation = new AdminAIConversation();
        var turn = new AdminAITurn();

        Assert.Equal(1, conversation.Version);
        Assert.Equal(0, conversation.LastSequence);
        Assert.Equal(AdminAIConversationStatus.Active, conversation.Status);
        Assert.Equal(1, turn.Version);
        Assert.Equal(AdminAITurnStatus.Queued, turn.Status);
        Assert.InRange(turn.ReadInvocationCount, 0, 6);
        Assert.InRange(turn.RedactedContextBytes, 0, 65_536);
    }

    [Theory]
    [InlineData(AdminAITurnStatus.Completed, true)]
    [InlineData(AdminAITurnStatus.Cancelled, true)]
    [InlineData(AdminAITurnStatus.Failed, true)]
    [InlineData(AdminAITurnStatus.AccessRevoked, true)]
    [InlineData(AdminAITurnStatus.Queued, false)]
    [InlineData(AdminAITurnStatus.Retrieving, false)]
    public void TurnTerminalHelper_IsClosedAndExplicit(AdminAITurnStatus status, bool terminal)
    {
        Assert.Equal(terminal, status.IsTerminal());
        Assert.Equal(!terminal, status.IsActive());
    }

    [Fact]
    public void AdminAIEntities_DoNotReferenceLiveSupportOrHumanChatEntities()
    {
        var entityTypes = typeof(AdminAIConversation).Assembly.GetTypes()
            .Where(type => type.Namespace == "NaderGorge.Domain.Entities.AdminAI");
        var referencedTypes = entityTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType).ToArray();

        Assert.DoesNotContain(referencedTypes, type => type.FullName?.Contains("LiveSupport", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(referencedTypes, type => type.Name is "ChatRoom" or "ChatMessage" or "ChatParticipant");
    }
}
