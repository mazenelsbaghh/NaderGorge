using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAITurnCompletionService(
    IAppDbContext db,
    IAdminAIAccessGate access,
    IAdminAIProposalBuilder proposals) : IAdminAITurnCompletionService
{
    private static readonly HashSet<string> ClarificationReasons = ["AMBIGUOUS_TARGET", "AMBIGUOUS_SCOPE", "AMBIGUOUS_PERIOD", "AMBIGUOUS_METRIC", "MISSING_REQUIRED_INPUT"];
    private static readonly HashSet<string> RefusalReasons = ["PROHIBITED_SECRET", "UNKNOWN_CAPABILITY", "POLICY_BYPASS", "RAW_DATABASE", "INFRASTRUCTURE", "UNSAFE_ATTACHMENT", "OUT_OF_SCOPE"];

    public async Task<AdminAITurnCompletionResult> CompleteAsync(Guid turnId, AdminAIInternalCompleteRequest request, CancellationToken ct)
    {
        var turn = await db.AdminAITurns.Include(x => x.Conversation).Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == turnId, ct)
            ?? throw new KeyNotFoundException(AdminAIErrorCodes.TurnNotFound);
        var step = turn.Steps.SingleOrDefault(x => x.StepNumber == request.ExpectedStepNumber)
            ?? throw new InvalidOperationException(AdminAIErrorCodes.StepVersionConflict);
        var canonical = Canonicalize(request.Decision);
        var decisionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        if (!FixedEquals(decisionHash, request.DecisionHash)) throw new InvalidOperationException(AdminAIErrorCodes.DecisionHashInvalid);
        if (step.CallbackStatus == "Delivered")
        {
            if (!FixedEquals(step.CanonicalDecisionHash, decisionHash)) throw new InvalidOperationException(AdminAIErrorCodes.IdempotencyPayloadConflict);
            var existing = await db.AdminAIActionProposals.AsNoTracking().Where(x => x.TurnId == turnId).Select(x => x.Id).ToListAsync(ct);
            return new(turnId, turn.Status, turn.Version, existing, true, false);
        }
        if (turn.Status.IsTerminal() || turn.CancellationRequestedAt is not null)
            return new(turnId, turn.Status, turn.Version, [], false, true);
        if (turn.Version != request.ExpectedTurnVersion || step.ExpectedTurnVersion > turn.Version) throw new InvalidOperationException(AdminAIErrorCodes.StepVersionConflict);
        await access.RequireCurrentAdminAsync(turn.ActorAdminUserId, checked((int)turn.ExpectedSecurityVersion), ct);
        var baseline = await db.AdminAICapabilityBaselines.AsNoTracking().SingleOrDefaultAsync(x => x.Id == turn.CapabilityBaselineId && x.Status == AdminAICapabilityBaselineStatus.Active, ct);
        var policy = await db.AdminAISensitiveDataPolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == turn.SensitiveDataPolicyVersionId && x.Status == AdminAISensitiveDataPolicyStatus.Active, ct);
        if (baseline?.Version != request.ExpectedBaselineVersion) throw new InvalidOperationException(AdminAIErrorCodes.BaselineChanged);
        if (policy?.Version != request.ExpectedSensitivePolicyVersion) throw new InvalidOperationException(AdminAIErrorCodes.SensitivePolicyChanged);

        using var document = JsonDocument.Parse(canonical, new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != "1") throw InvalidDecision();
        var type = root.GetProperty("type").GetString();
        if (type == "propose_actions") RequireExact(root, "schemaVersion", "type", "messageAr", "actions");
        else RequireExact(root, "schemaVersion", "type", Branch(root));
        var proposalIds = new List<Guid>();
        string visibleContent;
        string structured;
        switch (type)
        {
            case "answer":
                (visibleContent, structured) = await ValidateAnswerAsync(turnId, root.GetProperty("answer"), ct);
                turn.Status = AdminAITurnStatus.Completed;
                step.DecisionType = AdminAIModelDecisionType.Answer;
                break;
            case "clarify":
                (visibleContent, structured) = ValidateClarification(root.GetProperty("clarification"));
                turn.Status = AdminAITurnStatus.WaitingClarification;
                step.DecisionType = AdminAIModelDecisionType.Clarify;
                break;
            case "refuse":
                (visibleContent, structured) = ValidateRefusal(root.GetProperty("refusal"));
                turn.Status = AdminAITurnStatus.Completed;
                step.DecisionType = AdminAIModelDecisionType.Refuse;
                break;
            case "propose_actions":
                (visibleContent, structured, proposalIds) = await ValidateAndBuildProposalsAsync(turn, root, ct);
                turn.Status = AdminAITurnStatus.ProposalReady;
                step.DecisionType = AdminAIModelDecisionType.ProposeActions;
                break;
            case "request_reads":
                throw new InvalidOperationException(AdminAIErrorCodes.DecisionSchemaInvalid);
            default:
                throw InvalidDecision();
        }

        turn.Conversation.LastSequence++;
        turn.Conversation.LastActivityAt = DateTime.UtcNow;
        turn.Conversation.Version++;
        var message = new AdminAIMessage
        {
            ConversationId = turn.ConversationId,
            Sequence = turn.Conversation.LastSequence,
            Role = AdminAIMessageRole.Assistant,
            Content = visibleContent,
            StructuredContentJson = structured,
            TurnId = turn.Id
        };
        db.AdminAIMessages.Add(message);
        turn.OutputMessageId = message.Id;
        turn.Provider = request.Provider;
        turn.Model = request.Model;
        turn.ProviderResponseId = request.ProviderResponseId;
        turn.InputTokenCount = request.InputTokenCount;
        turn.OutputTokenCount = request.OutputTokenCount;
        turn.CompletedAt = DateTime.UtcNow;
        turn.Version++;
        step.CanonicalDecisionHash = decisionHash;
        step.Status = AdminAITurnStepStatus.Completed;
        step.CallbackStatus = "Delivered";
        step.CallbackAttemptCount++;
        step.Provider = request.Provider;
        step.Model = request.Model;
        step.ProviderResponseId = request.ProviderResponseId;
        step.InputTokenCount = request.InputTokenCount;
        step.OutputTokenCount = request.OutputTokenCount;
        step.LatencyMs = request.LatencyMs;
        step.CompletedAt = turn.CompletedAt;
        step.Version++;
        db.OutboxEvents.Add(new OutboxEvent { Type = "AdminAITurnCompleted", PayloadJson = JsonSerializer.Serialize(new { schemaVersion = "1", turnId, turn.ConversationId, sequence = turn.Conversation.LastSequence }) });
        await db.SaveChangesAsync(ct);
        return new(turnId, turn.Status, turn.Version, proposalIds, false, false);
    }

    private async Task<(string, string)> ValidateAnswerAsync(Guid turnId, JsonElement answer, CancellationToken ct)
    {
        RequireExact(answer, "summaryAr", "facts", "calculations", "inferences", "limitations", "suggestions", "evidenceInvocationIds");
        var summary = Text(answer, "summaryAr", 4000);
        Array(answer, "facts", 50, 1000); Array(answer, "calculations", 30, 1000); Array(answer, "inferences", 20, 1000); Array(answer, "limitations", 20, 1000); Array(answer, "suggestions", 20, 1000);
        var evidenceIds = Array(answer, "evidenceInvocationIds", 100, 100).Select(value => Guid.TryParse(value, out var id) ? id : throw InvalidDecision()).Distinct().ToArray();
        if (evidenceIds.Length > 0)
        {
            var owned = await db.AdminAIReadInvocations.AsNoTracking().CountAsync(x => x.TurnId == turnId && evidenceIds.Contains(x.Id) && (x.Status == AdminAIReadInvocationStatus.Succeeded || x.Status == AdminAIReadInvocationStatus.Empty || x.Status == AdminAIReadInvocationStatus.Truncated), ct);
            if (owned != evidenceIds.Length) throw new InvalidOperationException(AdminAIErrorCodes.DecisionEvidenceInvalid);
        }
        return (summary, answer.GetRawText());
    }

    private static (string, string) ValidateClarification(JsonElement value)
    {
        RequireExact(value, "questionAr", "reasonCode", "options");
        var question = Text(value, "questionAr", 2000);
        if (!ClarificationReasons.Contains(Text(value, "reasonCode", 64))) throw InvalidDecision();
        var options = value.GetProperty("options");
        if (options.ValueKind != JsonValueKind.Array || options.GetArrayLength() > 3) throw InvalidDecision();
        foreach (var option in options.EnumerateArray()) { RequireExact(option, "labelAr", "value"); Text(option, "labelAr", 200); Text(option, "value", 200); }
        return (question, value.GetRawText());
    }

    private static (string, string) ValidateRefusal(JsonElement value)
    {
        RequireExact(value, "reasonCode", "messageAr");
        if (!RefusalReasons.Contains(Text(value, "reasonCode", 64))) throw InvalidDecision();
        return (Text(value, "messageAr", 1000), value.GetRawText());
    }

    private async Task<(string, string, List<Guid>)> ValidateAndBuildProposalsAsync(AdminAITurn turn, JsonElement root, CancellationToken ct)
    {
        var message = Text(root, "messageAr", 2000);
        var actions = root.GetProperty("actions");
        if (actions.ValueKind != JsonValueKind.Array || actions.GetArrayLength() is < 1 or > 5) throw InvalidDecision();
        var suggestions = new List<AdminAIActionSuggestion>();
        foreach (var action in actions.EnumerateArray())
        {
            RequireExact(action, "clientActionId", "capabilityKey", "arguments", "safeIntentAr");
            Text(action, "safeIntentAr", 1000);
            suggestions.Add(new(Text(action, "clientActionId", 100), Text(action, "capabilityKey", 160), JsonSerializer.Deserialize<object>(action.GetProperty("arguments").GetRawText())!));
        }
        var built = await proposals.BuildManyAsync(turn.ActorAdminUserId, turn.Id, suggestions, ct);
        return (message, JsonSerializer.Serialize(new { actions = built.Select(x => x.Id) }), built.Select(x => x.Id).ToList());
    }

    private static string Branch(JsonElement root) => root.TryGetProperty("type", out var type) ? type.GetString() switch
    {
        "answer" => "answer", "clarify" => "clarification", "refuse" => "refusal", "propose_actions" => "messageAr", "request_reads" => "calls", _ => throw InvalidDecision()
    } : throw InvalidDecision();
    private static void RequireExact(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw InvalidDecision();
        var actual = value.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        if (actual.Count != names.Length || names.Any(name => !actual.Contains(name))) throw InvalidDecision();
    }
    private static string Text(JsonElement value, string property, int max)
    {
        var text = value.GetProperty(property).GetString()?.Trim();
        return !string.IsNullOrEmpty(text) && text.Length <= max ? text : throw InvalidDecision();
    }
    private static IReadOnlyList<string> Array(JsonElement value, string property, int maxItems, int maxText)
    {
        var array = value.GetProperty(property);
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > maxItems) throw InvalidDecision();
        return array.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String && x.GetString() is { } text && text.Length is > 0 && text.Length <= maxText ? text : throw InvalidDecision()).ToArray();
    }
    private static Exception InvalidDecision() => new InvalidOperationException(AdminAIErrorCodes.DecisionSchemaInvalid);
    private static bool FixedEquals(string? left, string? right) => left is not null && right is not null && left.Length == right.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static string Canonicalize(object decision)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(decision), new JsonDocumentOptions { MaxDepth = 8 });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, document.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject(); foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); } writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break;
            default: value.WriteTo(writer); break;
        }
    }
}
