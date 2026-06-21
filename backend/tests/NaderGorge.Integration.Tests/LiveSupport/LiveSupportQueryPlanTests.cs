using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class LiveSupportQueryPlanTests
{
    [Theory]
    [InlineData("live_support_queue_entries", "IX_live_support_queue_entries_DequeuedAt_EnteredAt_Sequence")]
    [InlineData("live_support_messages", "IX_live_support_messages_ConversationId_SentAt_Id")]
    [InlineData("live_support_events", "IX_live_support_events_ConversationId_Sequence")]
    public async Task ContractedLists_HaveSupportingIndexes(string table, string expectedIndex)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var indexes = await fixture.Db.Database.SqlQueryRaw<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = '{table}'").ToListAsync();
        Assert.Contains(indexes, x => x.Contains(expectedIndex, StringComparison.OrdinalIgnoreCase));
    }
}
