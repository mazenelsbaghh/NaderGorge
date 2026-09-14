using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Infrastructure.Services.AdminAI;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIToolGatewayTests
{
    [Fact]
    public async Task ActiveRead_ReturnsRedactedBoundedEvidence()
    {
        var definition = new AdminAICapabilityDefinition("safe.read", "1", "read", "read", "none", "{}", "{}", 10, 4096, 5000, "SafeQuery", []);
        var actor = Guid.NewGuid();
        var executor = new AdminAIReadCapabilityExecutor([new SafeRead()], new AdminAICapabilityRegistry([definition]), new AdminAISensitiveDataPolicy(), new AdminAIConversationTests.AllowAccess(actor));
        var result = await executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { }), default);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("visible", json); Assert.DoesNotContain("canary-secret", json); Assert.Contains("resultCount", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("unknown", "1")]
    [InlineData("safe.read", "2")]
    public async Task UnknownOrWrongVersion_ReadFailsClosed(string key, string version)
    {
        var definition = new AdminAICapabilityDefinition("safe.read", "1", "read", "read", "none", "{}", "{}", 10, 4096, 5000, "SafeQuery", []);
        var actor = Guid.NewGuid();
        var executor = new AdminAIReadCapabilityExecutor([new SafeRead()], new AdminAICapabilityRegistry([definition]), new AdminAISensitiveDataPolicy(), new AdminAIConversationTests.AllowAccess(actor));
        await Assert.ThrowsAsync<NotSupportedException>(() => executor.ExecuteAsync(actor, new AdminAIReadCall(key, version, new { }), default));
    }

    [Fact]
    public async Task DurableInvocation_ReplaysExactProtectedEnvelopeWithoutRunningAdapterAgain()
    {
        await using var db = Db(); var actor = Guid.NewGuid(); var turn = Guid.NewGuid(); var step = Guid.NewGuid(); var read = new CountingRead();
        var executor = DurableExecutor(actor, db, read);
        var call = new AdminAIReadCall("safe.read", "1", new { query = "visible" }, turn, step, 1, "trace-1");
        var first = await executor.ExecuteAsync(actor, call, default);
        var second = await executor.ExecuteAsync(actor, call, default);
        Assert.Equal(1, read.Calls);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first), System.Text.Json.JsonSerializer.Serialize(second));
        var stored = await db.AdminAIReadInvocations.SingleAsync();
        Assert.NotNull(stored.ProtectedResult); Assert.DoesNotContain("visible", System.Text.Encoding.UTF8.GetString(stored.ProtectedResult!));
        Assert.Equal(AdminAIReadInvocationStatus.Succeeded, stored.Status); Assert.Equal("trace-1", stored.TraceId);
    }

    [Fact]
    public async Task DurableInvocation_RejectsReplayWithDifferentArguments()
    {
        await using var db = Db(); var actor = Guid.NewGuid(); var read = new CountingRead(); var executor = DurableExecutor(actor, db, read);
        var turn = Guid.NewGuid(); var step = Guid.NewGuid();
        await executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { query = "one" }, turn, step, 1), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { query = "two" }, turn, step, 1), default));
        Assert.Equal(1, read.Calls);
    }

    [Fact]
    public async Task DurableInvocation_RevalidatesCachedOutputAgainstTheCurrentContract()
    {
        await using var db = Db();
        var actor = Guid.NewGuid();
        var turn = Guid.NewGuid();
        var step = Guid.NewGuid();
        var read = new CountingRead();
        var protector = Protector();
        const string originalOutputSchema = """
            {"type":"object","additionalProperties":false,"properties":{
              "label":{"type":"string"},
              "metadata":{"type":"object","additionalProperties":false,"properties":{}}
            }}
            """;
        var originalExecutor = new AdminAIReadCapabilityExecutor(
            [read],
            new AdminAICapabilityRegistry([Definition(outputSchema: originalOutputSchema)]),
            new AdminAISensitiveDataPolicy(),
            new AdminAIConversationTests.AllowAccess(actor),
            db,
            protector);
        var call = new AdminAIReadCall("safe.read", "1", new { }, turn, step, 1);
        await originalExecutor.ExecuteAsync(actor, call, default);

        const string narrowedOutputSchema = """
            {"type":"object","additionalProperties":false,"properties":{"label":{"type":"string"}}}
            """;
        var narrowedExecutor = new AdminAIReadCapabilityExecutor(
            [read],
            new AdminAICapabilityRegistry([Definition(outputSchema: narrowedOutputSchema)]),
            new AdminAISensitiveDataPolicy(),
            new AdminAIConversationTests.AllowAccess(actor),
            db,
            protector);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            narrowedExecutor.ExecuteAsync(actor, call, default));
        Assert.Equal(1, read.Calls);
    }

    [Fact]
    public async Task ResultRecordBudget_IsEnforcedAndFailureEvidenceIsDurable()
    {
        await using var db = Db(); var actor = Guid.NewGuid();
        var executor = DurableExecutor(actor, db, new CountingRead(resultCount: 11), maxRows: 10);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { }, Guid.NewGuid(), Guid.NewGuid(), 1), default));
        var stored = await db.AdminAIReadInvocations.SingleAsync();
        Assert.Equal(AdminAIReadInvocationStatus.Rejected, stored.Status); Assert.Equal("READ_REJECTED", stored.FailureCode);
    }

    [Fact]
    public async Task CapabilityTimeout_IsBoundedAtFiveSecondsAndRecorded()
    {
        await using var db = Db(); var actor = Guid.NewGuid();
        var executor = DurableExecutor(actor, db, new CountingRead(delay: TimeSpan.FromMilliseconds(100)), timeoutMs: 10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { }, Guid.NewGuid(), Guid.NewGuid(), 1), default));
        var stored = await db.AdminAIReadInvocations.SingleAsync();
        Assert.Equal(AdminAIReadInvocationStatus.Failed, stored.Status); Assert.Equal("READ_TIMEOUT", stored.FailureCode);
    }

    [Fact]
    public async Task CallerCancellation_IsRecordedAndAccessIsRecheckedForEveryInvocation()
    {
        await using var db = Db(); var actor = Guid.NewGuid(); var gate = new CountingAccess(actor); var read = new CountingRead(delay: TimeSpan.FromSeconds(1));
        var executor = DurableExecutor(actor, db, read, access: gate);
        using var cancellation = new CancellationTokenSource();
        var pending = executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { }, Guid.NewGuid(), Guid.NewGuid(), 1), cancellation.Token);
        while (read.Calls == 0) await Task.Delay(10);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, gate.Calls); Assert.Equal(AdminAIReadInvocationStatus.Cancelled, (await db.AdminAIReadInvocations.SingleAsync()).Status);
    }

    [Fact]
    public async Task UnknownInputField_IsRejectedBeforeAdapterDispatch()
    {
        var actor = Guid.NewGuid(); var read = new CountingRead();
        var definition = Definition(inputSchema: "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"additionalProperties\":false}");
        var executor = new AdminAIReadCapabilityExecutor([read], new AdminAICapabilityRegistry([definition]), new AdminAISensitiveDataPolicy(), new AdminAIConversationTests.AllowAccess(actor));
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { unexpected = true }), default));
        Assert.Equal(0, read.Calls);
    }

    [Fact]
    public async Task TypedReadSchema_RejectsMalformedSnapshotArgumentsBeforeAdapterDispatch()
    {
        const string schema = """
            {"type":"object","properties":{
              "studentId":{"type":"string","format":"uuid","minLength":36,"maxLength":36},
              "recentLimit":{"type":"integer","minimum":0,"maximum":10},
              "selection":{"type":"object","minProperties":1,"maxProperties":2,"additionalProperties":false,"properties":{
                "profile":{"type":"object","additionalProperties":false,"properties":{
                  "fields":{"type":"array","minItems":1,"maxItems":2,"uniqueItems":true,"items":{"type":"string","enum":["account","academic"]}}
                },"required":["fields"]},
                "balances":{"type":"object","additionalProperties":false,"properties":{
                  "teacherId":{"type":"string","format":"uuid","minLength":36,"maxLength":36}
                }}
              }}
            },"required":["studentId","selection","recentLimit"],"additionalProperties":false}
            """;
        var validId = Guid.NewGuid().ToString("D");
        string[] invalidJson =
        [
            "{\"studentId\":7,\"selection\":{\"profile\":{\"fields\":[\"account\"]}},\"recentLimit\":1}",
            "{\"studentId\":\"bad\",\"selection\":{\"profile\":{\"fields\":[\"account\"]}},\"recentLimit\":1}",
            $"{{\"studentId\":\"{Guid.Empty:D}\",\"selection\":{{\"profile\":{{\"fields\":[\"account\"]}}}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"unknown\":{{}}}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[]}}}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[\"account\",\"account\"]}}}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[\"unknown\"]}}}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[\"account\"],\"extra\":true}}}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"balances\":{{\"teacherId\":\"bad\"}}}},\"recentLimit\":1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[\"account\"]}}}},\"recentLimit\":-1}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[\"account\"]}}}},\"recentLimit\":11}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[\"account\"]}}}},\"recentLimit\":1.5}}",
            $"{{\"studentId\":\"{validId}\",\"selection\":{{\"profile\":{{\"fields\":[\"account\"]}}}},\"recentLimit\":1,\"extra\":true}}"
        ];

        foreach (var invalid in invalidJson)
        {
            var actor = Guid.NewGuid();
            var read = new CountingRead();
            var executor = new AdminAIReadCapabilityExecutor(
                [read],
                new AdminAICapabilityRegistry([Definition(inputSchema: schema)]),
                new AdminAISensitiveDataPolicy(),
                new AdminAIConversationTests.AllowAccess(actor));
            var input = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(invalid);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", input), default));
            Assert.Equal(0, read.Calls);
        }
    }

    [Fact]
    public async Task ClosedOutputSchema_RejectsUnexpectedProviderVisibleFields()
    {
        const string outputSchema = """
            {"type":"object","additionalProperties":false,"properties":{"label":{"type":"string"}}}
            """;
        var actor = Guid.NewGuid();
        var read = new CountingRead();
        var definition = Definition(outputSchema: outputSchema);
        var executor = new AdminAIReadCapabilityExecutor(
            [read],
            new AdminAICapabilityRegistry([definition]),
            new AdminAISensitiveDataPolicy(),
            new AdminAIConversationTests.AllowAccess(actor));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(actor, new AdminAIReadCall("safe.read", "1", new { }), default));

        Assert.Equal(1, read.Calls);
    }

    [Fact]
    public async Task LocalSchemaReference_IsResolvedForNestedOutputValidation()
    {
        const string outputSchema = """
            {"type":"object","additionalProperties":false,"properties":{
              "label":{"type":"string"},"metadata":{"$ref":"#/$defs/metadata"}
             },"$defs":{"metadata":{"type":"object","additionalProperties":false,"properties":{}}}}
            """;
        var actor = Guid.NewGuid();
        var read = new CountingRead();
        var definition = Definition(outputSchema: outputSchema);
        var executor = new AdminAIReadCapabilityExecutor(
            [read],
            new AdminAICapabilityRegistry([definition]),
            new AdminAISensitiveDataPolicy(),
            new AdminAIConversationTests.AllowAccess(actor));

        var result = await executor.ExecuteAsync(
            actor,
            new AdminAIReadCall("safe.read", "1", new { }),
            default);

        Assert.Equal(1, read.Calls);
        Assert.Contains("\"label\":\"visible\"", System.Text.Json.JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    private static AdminAICapabilityDefinition Definition(
        int maxRows = 10,
        int timeoutMs = 5000,
        string inputSchema = "{}",
        string outputSchema = "{}") =>
        new("safe.read", "1", "read", "read", "none", inputSchema, outputSchema, maxRows, 4096, timeoutMs, "SafeQuery", []);

    private static AdminAIReadCapabilityExecutor DurableExecutor(Guid actor, AppDbContext db, IAdminAIReadCapability read, int maxRows = 10, int timeoutMs = 5000, IAdminAIAccessGate? access = null) =>
        new([read], new AdminAICapabilityRegistry([Definition(maxRows, timeoutMs)]), new AdminAISensitiveDataPolicy(), access ?? new AdminAIConversationTests.AllowAccess(actor), db, Protector());

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-read-{Guid.NewGuid()}").Options);
    private static AdminAIDataProtector Protector()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminAI:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }).Build();
        return new AdminAIDataProtector(new EphemeralDataProtectionProvider(), configuration);
    }

    private sealed record SafeOutput(string Label, IReadOnlyDictionary<string, string> Metadata);
    private sealed class SafeRead : IAdminAIReadCapability
    {
        public string Key => "safe.read"; public Type OutputType => typeof(SafeOutput);
        public Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct) => Task.FromResult(new AdminAIReadCapabilityResult(new SafeOutput("visible", new Dictionary<string, string> { ["apiKey"] = "canary-secret" }), 1, true, false, DateTime.UtcNow, []));
    }

    private sealed class CountingRead(int resultCount = 1, TimeSpan? delay = null) : IAdminAIReadCapability
    {
        public int Calls { get; private set; }
        public string Key => "safe.read"; public Type OutputType => typeof(SafeOutput);
        public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
        {
            Calls++; if (delay.HasValue) await Task.Delay(delay.Value, ct);
            return new AdminAIReadCapabilityResult(new SafeOutput("visible", new Dictionary<string, string>()), resultCount, true, false, DateTime.UtcNow, ["safe-ref"]);
        }
    }

    private sealed class CountingAccess(Guid actor) : IAdminAIAccessGate
    {
        public int Calls { get; private set; }
        public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion, CancellationToken cancellationToken)
        {
            Calls++; if (userId != actor) throw new UnauthorizedAccessException();
            return Task.FromResult(new AdminAIAccessSnapshot(userId, 1, DateTime.UtcNow));
        }
    }
}
