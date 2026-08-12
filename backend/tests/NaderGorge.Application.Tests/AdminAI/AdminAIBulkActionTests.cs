using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIBulkActionTests
{
    [Fact]
    public async Task Preview_FreezesSortedMembershipVersionsExclusionsAndCount()
    {
        await using var db = CreateDb();
        var source = new MembershipSource([new("b", "v1"), new("a", "v3"), new("c", "v2")]);
        var executor = new AdminAIBulkActionExecutor("admin.bulk.test", db, source, new ItemOperation());
        var input = Input(AdminAIBulkExecutionMode.Partial, "c");

        var first = await executor.PreviewAsync(Guid.NewGuid(), input, default);
        source.Items = [new("a", "v3"), new("b", "v1"), new("c", "v2")];
        var reordered = await executor.PreviewAsync(Guid.NewGuid(), input, default);
        source.Items = [new("a", "v4"), new("b", "v1"), new("c", "v2")];
        var changed = await executor.PreviewAsync(Guid.NewGuid(), input, default);

        Assert.Equal(first.StateFingerprint, reordered.StateFingerprint);
        Assert.NotEqual(first.StateFingerprint, changed.StateFingerprint);
        Assert.Contains("\"selectedCount\":2", JsonSerializer.Serialize(first.Effect));
        Assert.DoesNotContain("\"c\"", JsonSerializer.Serialize(first.Effect));
    }

    [Fact]
    public async Task Preview_RejectsDuplicateOrUnboundedMembership()
    {
        await using var db = CreateDb();
        var duplicate = new AdminAIBulkActionExecutor("admin.bulk.test", db,
            new MembershipSource([new("a", "1"), new("a", "1")]), new ItemOperation());
        await Assert.ThrowsAsync<InvalidOperationException>(() => duplicate.PreviewAsync(Guid.NewGuid(), Input(), default));

        var tooMany = Enumerable.Range(0, 10_001).Select(x => new AdminAIBulkCandidate($"item-{x}", "1")).ToArray();
        var unbounded = new AdminAIBulkActionExecutor("admin.bulk.test", db, new MembershipSource(tooMany), new ItemOperation());
        await Assert.ThrowsAsync<InvalidOperationException>(() => unbounded.PreviewAsync(Guid.NewGuid(), Input(), default));
    }

    [Fact]
    public async Task Partial_UsesStablePerItemIdsAndReconcilesEveryTerminalCount()
    {
        await using var db = CreateDb();
        var operation = new ItemOperation(new Dictionary<string, AdminAIExecutionItemStatus>
        {
            ["a"] = AdminAIExecutionItemStatus.Succeeded,
            ["b"] = AdminAIExecutionItemStatus.Skipped,
            ["c"] = AdminAIExecutionItemStatus.ValidationFailed
        });
        var executor = new AdminAIBulkActionExecutor("admin.bulk.test", db,
            new MembershipSource([new("c", "1"), new("a", "1"), new("b", "1")]), operation);

        var result = await executor.ExecuteAsync(Guid.NewGuid(), Input(), "execution", default);

        Assert.Equal(AdminAIExecutionStatus.PartiallySucceeded, result.Status);
        Assert.Equal(1, result.AffectedCount);
        var json = JsonSerializer.Serialize(result.SafeResult);
        Assert.Contains("\"selected\":3", json); Assert.Contains("\"succeeded\":1", json);
        Assert.Contains("\"skipped\":1", json); Assert.Contains("\"failed\":1", json);
        Assert.Equal(["execution:000000", "execution:000001", "execution:000002"], operation.OperationIds);
    }

    [Fact]
    public async Task Atomic_FailsClosedWithoutRelationalTransaction()
    {
        await using var db = CreateDb();
        var operation = new ItemOperation();
        var executor = new AdminAIBulkActionExecutor("admin.bulk.test", db,
            new MembershipSource([new("a", "1")]), operation);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(Guid.NewGuid(), Input(AdminAIBulkExecutionMode.Atomic), "execution", default));
        Assert.Empty(operation.OperationIds);
    }

    private static AdminAIBulkActionInput Input(AdminAIBulkExecutionMode mode = AdminAIBulkExecutionMode.Partial, params string[] excluded) =>
        new(JsonSerializer.SerializeToElement(new { status = "active" }), excluded, mode);

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-bulk-{Guid.NewGuid()}").Options);

    private sealed class MembershipSource(IReadOnlyList<AdminAIBulkCandidate> items) : IAdminAIBulkMembershipSource
    {
        public IReadOnlyList<AdminAIBulkCandidate> Items { get; set; } = items;
        public Task<AdminAIBulkMembership> ResolveAsync(string capabilityKey, Guid actorId, JsonElement selector, CancellationToken cancellationToken) =>
            Task.FromResult(new AdminAIBulkMembership(Items, DateTime.UtcNow));
    }

    private sealed class ItemOperation(IReadOnlyDictionary<string, AdminAIExecutionItemStatus>? statuses = null) : IAdminAIBulkItemOperation
    {
        public string CapabilityKey => "admin.bulk.test";
        public List<string> OperationIds { get; } = [];
        public Task<AdminAIBulkItemOutcome> ExecuteAsync(Guid actorId, string safeItemReference, string operationId, CancellationToken cancellationToken)
        {
            OperationIds.Add(operationId);
            var status = statuses?.GetValueOrDefault(safeItemReference) ?? AdminAIExecutionItemStatus.Succeeded;
            return Task.FromResult(new AdminAIBulkItemOutcome(status, new { reference = safeItemReference }, status == AdminAIExecutionItemStatus.Succeeded ? null : "item_not_changed"));
        }
    }
}
