using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Security;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAICatalogTests
{
    [Fact]
    public void Registry_IsDeterministicImmutableAndRejectsUnknownCapabilities()
    {
        var input = new[] { Read("read.users"), Action("user.note.update", "ordinary", "ordinary") };
        var first = new AdminAICapabilityRegistry(input);
        var second = new AdminAICapabilityRegistry(input.Reverse());

        Assert.Equal(first.BaselineHash, second.BaselineHash);
        Assert.False(first.TryGet("unknown", out _));
        Assert.Throws<NotSupportedException>(() => ((IList<AdminAICapabilityDefinition>)first.All).Add(Read("extra")));
    }

    [Fact]
    public void RegistryHash_ChangesWhenOnlyAContractOrLimitChanges()
    {
        var baseline = Read("read.users");
        var original = new AdminAICapabilityRegistry([baseline]);
        var changedInput = new AdminAICapabilityRegistry([baseline with { InputSchema = "input:v2" }]);
        var changedOutput = new AdminAICapabilityRegistry([baseline with { OutputSchema = "output:v2" }]);
        var changedLimit = new AdminAICapabilityRegistry([baseline with { MaxRows = baseline.MaxRows + 1 }]);

        Assert.NotEqual(original.BaselineHash, changedInput.BaselineHash);
        Assert.NotEqual(original.BaselineHash, changedOutput.BaselineHash);
        Assert.NotEqual(original.BaselineHash, changedLimit.BaselineHash);
    }

    [Theory]
    [InlineData("read", "read", "ordinary")]
    [InlineData("action", "strong", "ordinary")]
    [InlineData("action", "read", "none")]
    [InlineData("delete", "strong", "strong")]
    public void Registry_FailsClosedForInvalidRiskOrConfirmation(string kind, string risk, string confirmation)
    {
        var item = Read("invalid") with { Kind = kind, Risk = risk, Confirmation = confirmation };
        Assert.Throws<InvalidOperationException>(() => new AdminAICapabilityRegistry([item]));
    }

    [Fact]
    public void Registry_RejectsDuplicateKeysAndUnsafeLimits()
    {
        Assert.Throws<InvalidOperationException>(() => new AdminAICapabilityRegistry([Read("same"), Read("same")]));
        Assert.Throws<InvalidOperationException>(() => new AdminAICapabilityRegistry([Read("large") with { MaxBytes = 2_000_000 }]));
    }

    [Fact]
    public void ProductionReadRegistry_ExposesOnlyTheReviewedReadCapabilities()
    {
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();

        Assert.Equal(22, registry.All.Count);
        Assert.All(registry.All, capability =>
        {
            Assert.Equal("read", capability.Kind);
            Assert.Equal("none", capability.Confirmation);
            Assert.Contains("\"additionalProperties\":false", capability.InputSchema, StringComparison.Ordinal);
        });
        Assert.True(registry.TryGet("identity.users.summary", out _));
        Assert.True(registry.TryGet("platform-finance.summary", out _));
        Assert.True(registry.TryGet("teachers.search", out var teacherSearch));
        Assert.Equal("1.1.0", teacherSearch.Version);
        Assert.Equal(3, teacherSearch.MaxRows);
        Assert.Contains("\"query\"", teacherSearch.InputSchema, StringComparison.Ordinal);
        Assert.Contains("\"additionalProperties\":false", teacherSearch.OutputSchema, StringComparison.Ordinal);
        Assert.True(registry.TryGet("teacher.subscribers.summary", out var teacherSubscribers));
        Assert.Equal("1.1.0", teacherSubscribers.Version);
        Assert.Equal(1, teacherSubscribers.MaxRows);
        Assert.Contains("\"teacherId\"", teacherSubscribers.InputSchema, StringComparison.Ordinal);
        Assert.Contains("\"nonCancelledHistorical\"", teacherSubscribers.OutputSchema, StringComparison.Ordinal);
        Assert.True(registry.TryGet("students.search", out var studentSearch));
        Assert.Equal("1.1.0", studentSearch.Version);
        Assert.Equal(5, studentSearch.MaxRows);
        Assert.Contains("\"additionalProperties\":false", studentSearch.OutputSchema, StringComparison.Ordinal);
        Assert.True(registry.TryGet("student.snapshot", out var studentSnapshot));
        Assert.Equal("1.1.0", studentSnapshot.Version);
        Assert.Equal(1, studentSnapshot.MaxRows);
        Assert.Contains("\"maximum\":10", studentSnapshot.InputSchema, StringComparison.Ordinal);
        Assert.Contains("\"uniqueItems\":true", studentSnapshot.InputSchema, StringComparison.Ordinal);
        Assert.Contains("\"selection\"", studentSnapshot.InputSchema, StringComparison.Ordinal);
        Assert.Contains("\"minProperties\":1", studentSnapshot.InputSchema, StringComparison.Ordinal);
        Assert.Contains("\"studentPhones\"", studentSnapshot.InputSchema, StringComparison.Ordinal);
        Assert.Contains("\"contextTeacherEntitlement\"", studentSnapshot.OutputSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sections\"", studentSnapshot.InputSchema, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitivePolicy_RejectsProhibitedNamesAndBinaryOrSecureTypes()
    {
        var policy = new AdminAISensitiveDataPolicy();
        Assert.Throws<InvalidOperationException>(() => policy.AssertSafeSchema(typeof(UnsafePasswordProjection)));
        Assert.Throws<InvalidOperationException>(() => policy.AssertSafeSchema(typeof(UnsafeBinaryProjection)));
        policy.AssertSafeSchema(typeof(SafeProjection));
    }

    [Fact]
    public void SensitivePolicy_RecursivelyRedactsJsonAndHasStableHash()
    {
        var first = new AdminAISensitiveDataPolicy();
        var second = new AdminAISensitiveDataPolicy();
        var result = first.RedactJson("{\"name\":\"Mazen\",\"nested\":{\"refresh_token\":\"canary\"},\"items\":[{\"apiKey\":\"canary2\"}]}");

        Assert.Equal(first.PolicyHash, second.PolicyHash);
        Assert.Contains("Mazen", result, StringComparison.Ordinal);
        Assert.DoesNotContain("canary", result, StringComparison.Ordinal);
        Assert.Equal(2, result.Split("[REDACTED]", StringSplitOptions.None).Length - 1);
    }

    private static AdminAICapabilityDefinition Read(string key) =>
        new(key, "1", "read", "read", "none", "input:v1", "output:v1", 100, 65_536, 5_000, "Query.Handler", ["users"]);

    private static AdminAICapabilityDefinition Action(string key, string risk, string confirmation) =>
        new(key, "1", "action", risk, confirmation, "input:v1", "output:v1", 1, 65_536, 5_000, "Command.Handler", ["users"]);

    private sealed record SafeProjection(Guid Id, string DisplayName, IReadOnlyList<SafeChild> Children);
    private sealed record SafeChild(int Count);
    private sealed record UnsafePasswordProjection(string PasswordHash);
    private sealed record UnsafeBinaryProjection(byte[] Payload);
}
