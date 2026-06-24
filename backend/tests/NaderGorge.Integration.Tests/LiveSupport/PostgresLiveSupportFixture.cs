using Microsoft.EntityFrameworkCore;
using NaderGorge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Linq;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class PostgresLiveSupportFixture : IAsyncDisposable
{
    static PostgresLiveSupportFixture() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    public PostgresLiveSupportFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("PostgreSQL integration tests require ConnectionStrings__DefaultConnection and never use EF InMemory.");
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
    }
    public string ConnectionString { get; }
    public AppDbContext Db { get; }

    private static bool _migrated = false;
    private static readonly object _lock = new();

    public async Task ResetAsync() 
    { 
        lock (_lock)
        {
            if (!_migrated)
            {
                Npgsql.NpgsqlConnection.ClearAllPools();
                Db.Database.EnsureDeleted();
                Db.Database.Migrate();
                _migrated = true;
            }
        }

        Npgsql.NpgsqlConnection.ClearAllPools();
        
        var tables = new[]
        {
            "live_support_queue_entries",
            "live_support_assignments",
            "live_support_conversations",
            "live_support_messages",
            "live_support_events",
            "live_support_ratings",
            "live_support_action_executions",
            "live_support_ai_turns",
            "live_support_ai_conversation_states",
            "live_support_ai_pending_actions",
            "live_support_ai_verification_policy_questions",
            "live_support_ai_verification_sessions",
            "live_support_ai_verification_attempts",
            "live_support_ai_policy_versions",
            "live_support_ai_knowledge_entries",
            "live_support_ai_knowledge_revisions",
            "live_support_ai_policy_knowledge_revisions"
        };

        var query = "TRUNCATE TABLE " + string.Join(", ", tables.Select(t => "\"" + t + "\"")) + " CASCADE;";
        await Db.Database.ExecuteSqlRawAsync(query);
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
