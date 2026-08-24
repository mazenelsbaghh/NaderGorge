using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAITurnCompletionServiceTests
{
    [Fact]
    public async Task Answer_RequiresEvidenceOwnedBySameTurn()
    {
        await using var db = CreateDb();
        var state = await SeedAsync(db);
        var foreignEvidence = new AdminAIReadInvocation { TurnId = Guid.NewGuid(), TurnStepId = Guid.NewGuid(), InvocationSequence = 1, Status = AdminAIReadInvocationStatus.Succeeded };
        db.Add(foreignEvidence); await db.SaveChangesAsync();
        var decision = Json(JsonSerializer.Serialize(new { schemaVersion = "1", type = "answer", answer = new { summaryAr = "إجابة", facts = Array.Empty<string>(), calculations = Array.Empty<string>(), inferences = Array.Empty<string>(), limitations = Array.Empty<string>(), suggestions = Array.Empty<string>(), evidenceInvocationIds = new[] { foreignEvidence.Id.ToString() } } }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CompleteAsync(state.Turn.Id, Request(state, decision), default));

        Assert.Equal(AdminAIErrorCodes.DecisionEvidenceInvalid, exception.Message);
        Assert.Empty(db.AdminAIMessages.Where(x => x.Role == AdminAIMessageRole.Assistant));
    }

    [Fact]
    public async Task Completion_ProductionRegression_August21_AcceptsWorkerHashForArabicAnswer()
    {
        await using var db = CreateDb();
        var state = await SeedAsync(db);
        var evidenceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        db.Add(new AdminAIReadInvocation
        {
            Id = evidenceId,
            TurnId = state.Turn.Id,
            TurnStepId = state.Turn.Steps.Single().Id,
            InvocationSequence = 1,
            Status = AdminAIReadInvocationStatus.Succeeded
        });
        await db.SaveChangesAsync();
        var decision = Json("{\"schemaVersion\":\"1\",\"type\":\"answer\",\"answer\":{\"summaryAr\":\"عدد الطلاب 10 😀\",\"facts\":[\"سطر\\u2028جديد\"],\"calculations\":[],\"inferences\":[],\"limitations\":[],\"suggestions\":[],\"evidenceInvocationIds\":[\"11111111-1111-4111-8111-111111111111\"]}}");
        var request = Request(state, decision) with
        {
            DecisionHash = "f0af81c51fddb9880cd4de4248b87a1aac7c5586824ed5b25a2ca0f90861a185"
        };

        var completion = await Service(db).CompleteAsync(state.Turn.Id, request, default);

        Assert.Equal(AdminAITurnStatus.Completed, completion.Status);
        Assert.Contains(db.AdminAIMessages, message => message.Role == AdminAIMessageRole.Assistant && message.Content == "عدد الطلاب 10 😀");
        var realtimeEvent = Assert.Single(db.OutboxEvents.Where(outbox => outbox.Type == AdminAIOutboxQueueDispatcher.RealtimeEventType));
        Assert.Equal(state.Turn.ActorAdminUserId.ToString(), realtimeEvent.TargetUserId);
        Assert.Null(realtimeEvent.TargetGroup);
        Assert.NotNull(AdminAIOutboxQueueDispatcher.ValidateRealtimeEnvelope(realtimeEvent));
    }

    [Fact]
    public async Task Refusal_IsPersistedOnceAndMatchingReplayReturnsOriginalOutcome()
    {
        await using var db = CreateDb();
        var state = await SeedAsync(db);
        var decision = Json("{\"schemaVersion\":\"1\",\"type\":\"refuse\",\"refusal\":{\"reasonCode\":\"RAW_DATABASE\",\"messageAr\":\"لا يمكن تنفيذ SQL مباشر.\"}}");
        var request = Request(state, decision);

        var first = await Service(db).CompleteAsync(state.Turn.Id, request, default);
        var replay = await Service(db).CompleteAsync(state.Turn.Id, request, default);

        Assert.Equal(AdminAITurnStatus.Completed, first.Status);
        Assert.True(replay.Replayed);
        Assert.Single(db.AdminAIMessages.Where(x => x.Role == AdminAIMessageRole.Assistant));
    }

    [Fact]
    public async Task LateCallbackAfterCancellation_IsDiscardedWithoutTranscript()
    {
        await using var db = CreateDb();
        var state = await SeedAsync(db);
        state.Turn.Status = AdminAITurnStatus.Cancelled; state.Turn.CompletedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        var decision = Json("{\"schemaVersion\":\"1\",\"type\":\"refuse\",\"refusal\":{\"reasonCode\":\"OUT_OF_SCOPE\",\"messageAr\":\"مرفوض\"}}");

        var result = await Service(db).CompleteAsync(state.Turn.Id, Request(state, decision), default);

        Assert.True(result.Discarded);
        Assert.Empty(db.AdminAIMessages.Where(x => x.Role == AdminAIMessageRole.Assistant));
    }

    private static AdminAITurnCompletionService Service(AppDbContext db) => new(db, new AdminAIAccessGate(db), new RejectingProposalBuilder());
    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-completion-{Guid.NewGuid()}").Options);
    private static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();
    private static AdminAIInternalCompleteRequest Request(State state, JsonElement decision) => new("1", "lease", state.Turn.Version, 1, state.Baseline.Version, state.Policy.Version, decision, Hash(decision), "callback", "gemini-developer", "test-model", null, null, null, 10);
    private static string Hash(JsonElement decision)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) Canonical(writer, decision);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
    private static void Canonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var item in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(item.Name); Canonical(writer, item.Value); } writer.WriteEndObject(); }
        else if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Canonical(writer, item); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
    private static async Task<State> SeedAsync(AppDbContext db)
    {
        var user = new User { FullName = "Admin", PhoneNumber = Guid.NewGuid().ToString("N"), PasswordHash = "unused", SecurityStampVersion = 3 };
        var role = new Role { Name = "Admin", Type = RoleType.Admin };
        var baseline = new AdminAICapabilityBaseline { Version = "base-1", ManifestHash = new string('a', 64), RuntimeInventoryHash = new string('b', 64), FrontendInventoryHash = new string('c', 64), Status = AdminAICapabilityBaselineStatus.Active };
        var policy = new AdminAISensitiveDataPolicyVersion { Version = "policy-1", PolicyHash = new string('d', 64), Status = AdminAISensitiveDataPolicyStatus.Active };
        var conversation = new AdminAIConversation { OwnerAdminUserId = user.Id, Title = "Test" };
        var turn = new AdminAITurn { Conversation = conversation, ConversationId = conversation.Id, ActorAdminUserId = user.Id, CapabilityBaselineId = baseline.Id, SensitiveDataPolicyVersionId = policy.Id, ExpectedSecurityVersion = 3, CallbackIdempotencyDigest = new string('e', 64), Status = AdminAITurnStatus.Answering, CurrentStepNumber = 1 };
        var step = new AdminAITurnStep { Turn = turn, TurnId = turn.Id, StepNumber = 1, Status = AdminAITurnStepStatus.ProviderRunning, ExpectedTurnVersion = 1 };
        db.AddRange(user, role, new UserRole { User = user, Role = role }, baseline, policy, conversation, turn, step); await db.SaveChangesAsync();
        return new(turn, baseline, policy);
    }
    private sealed record State(AdminAITurn Turn, AdminAICapabilityBaseline Baseline, AdminAISensitiveDataPolicyVersion Policy);
    private sealed class RejectingProposalBuilder : IAdminAIProposalBuilder
    {
        public Task<AdminAIProposalDto> BuildAsync(Guid actorId, Guid turnId, string capabilityKey, object input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdminAIProposalDto>> BuildManyAsync(Guid actorId, Guid turnId, IReadOnlyList<AdminAIActionSuggestion> suggestions, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
