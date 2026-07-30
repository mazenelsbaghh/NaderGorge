using System.Text.Json;
using NaderGorge.Infrastructure.Background;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class RedisJobEnqueuerMappingTests
{
    [Fact]
    public void Live_support_mapping_is_explicit_and_uses_deterministic_turn_job_id()
    {
        var turnId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { turnId, conversationId = Guid.NewGuid() });

        Assert.Equal("live support turn", RedisJobEnqueuer.ResolveJobType("ai-live-support-turns", "respond"));
        Assert.Equal($"turn-{turnId:D}", RedisJobEnqueuer.ResolveStableJobId("ai-live-support-turns", payload));
    }

    [Fact]
    public void Unknown_queue_or_job_fails_closed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RedisJobEnqueuer.ResolveJobType("ai-live-support-turns", "unknown"));
        Assert.Throws<InvalidOperationException>(() =>
            RedisJobEnqueuer.ResolveJobType("unknown", "respond"));
    }

    [Fact]
    public void Live_support_payload_without_valid_turn_id_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RedisJobEnqueuer.ResolveStableJobId("ai-live-support-turns", "{\"conversationId\":\"not-a-turn\"}"));
    }
}
