using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Integration.Tests.LiveSupport;
using System;
using System.Threading.Tasks;
using Xunit;

namespace NaderGorge.Integration.Tests.LiveSupportAI;

public sealed class LiveSupportAIQueryPlanTests
{
    private static async Task<string> ExplainQueryAsync(DbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync();
        }

        using var setCmd = connection.CreateCommand();
        setCmd.CommandText = "SET enable_seqscan = off;";
        if (db.Database.CurrentTransaction != null)
        {
            setCmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        }
        await setCmd.ExecuteNonQueryAsync();

        using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN {sql}";
        if (db.Database.CurrentTransaction != null)
        {
            command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        }
        using var reader = await command.ExecuteReaderAsync();
        var plan = "";
        while (await reader.ReadAsync())
        {
            plan += reader.GetString(0) + "\n";
        }
        return plan;
    }

    [Fact]
    public async Task QueryPlans_UseExpectedIndexes()
    {
        Npgsql.NpgsqlConnection.ClearAllPools();
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        // 1. Stale turns query plan
        var staleTurnsPlan = await ExplainQueryAsync(fixture.Db, 
            "SELECT * FROM live_support_ai_turns WHERE \"Status\" = 0 AND \"QueuedAt\" < '2026-06-24 00:00:00'::timestamp");
        Assert.Contains("Index Scan", staleTurnsPlan);
        Assert.Contains("IX_live_support_ai_turns_Status_QueuedAt", staleTurnsPlan);

        // 2. Callback schedule query plan
        var callbackSchedulePlan = await ExplainQueryAsync(fixture.Db,
            "SELECT * FROM live_support_ai_turns WHERE \"CallbackStatus\" = 1 AND \"NextCallbackAttemptAt\" < '2026-06-24 00:00:00'::timestamp");
        Assert.Contains("Index Scan", callbackSchedulePlan);
        Assert.Contains("IX_live_support_ai_turns_CallbackStatus_NextCallbackAttemptAt", callbackSchedulePlan);

        // 3. Pending action expiry query plan
        var pendingActionPlan = await ExplainQueryAsync(fixture.Db,
            "SELECT * FROM live_support_ai_pending_actions WHERE \"Status\" = 0 AND \"ExpiresAt\" < '2026-06-24 00:00:00'::timestamp");
        Assert.Contains("Index Scan", pendingActionPlan);
        Assert.True(
            pendingActionPlan.Contains("IX_live_support_ai_pending_actions_Status_ExpiresAt") ||
            pendingActionPlan.Contains("IX_live_support_ai_pending_actions_ConversationId_DecisionKind"),
            $"Expected status index or partial index scan but got: {pendingActionPlan}");

        // 4. Verification expiry query plan
        var verificationExpiryPlan = await ExplainQueryAsync(fixture.Db,
            "SELECT * FROM live_support_ai_verification_sessions WHERE \"Status\" = 0 AND \"ExpiresAt\" < '2026-06-24 00:00:00'::timestamp");
        Assert.Contains("Index Scan", verificationExpiryPlan);
        Assert.Contains("IX_live_support_ai_verification_sessions_Status_ExpiresAt", verificationExpiryPlan);

        // 5. Knowledge retrieval query plan
        var knowledgeRevisionPlan = await ExplainQueryAsync(fixture.Db,
            "SELECT * FROM live_support_ai_knowledge_revisions WHERE \"EntryId\" = '00000000-0000-0000-0000-000000000000'::uuid AND \"RevisionNumber\" = 1");
        Assert.Contains("Index Scan", knowledgeRevisionPlan);
        Assert.Contains("IX_live_support_ai_knowledge_revisions_EntryId_RevisionNumber", knowledgeRevisionPlan);

        // 6. Participant snapshot: message query by conversation
        var messagesPlan = await ExplainQueryAsync(fixture.Db,
            "SELECT * FROM live_support_messages WHERE \"ConversationId\" = '00000000-0000-0000-0000-000000000000'::uuid");
        Assert.Contains("Index Scan", messagesPlan);
        Assert.True(
            messagesPlan.Contains("IX_live_support_messages_ConversationId") ||
            messagesPlan.Contains("IX_live_support_messages_ConversationId_SentAt_Id"),
            $"Expected conversation message index scan but got: {messagesPlan}");
    }
}
