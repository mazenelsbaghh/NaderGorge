using System.Text.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Services;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAITurnOrchestrator(
    IAppDbContext db,
    ILiveSupportAIContextBuilder contextBuilder,
    ILiveSupportAIDataProtector? dataProtector = null) : ILiveSupportAITurnOrchestrator
{
    public async Task QueueForParticipantMessageAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var state = await db.LiveSupportAIConversationStates
            .SingleOrDefaultAsync(item => item.ConversationId == conversationId, cancellationToken);
        if (state is null || state.Mode != LiveSupportAIMode.AiActive) return;
        if (!await db.LiveSupportAIPolicyVersions.AnyAsync(item => item.Id == state.PolicyVersionId && item.Status == LiveSupportAIPolicyStatus.Published && item.IsEnabled, cancellationToken)) return;
        if (await db.LiveSupportAITurns.AnyAsync(item => item.SourceMessageId == messageId, cancellationToken)) return;

        var conversation = await db.LiveSupportConversations
            .SingleAsync(item => item.Id == conversationId, cancellationToken);
        var now = DateTime.UtcNow;
        var turn = new LiveSupportAITurn
        {
            ConversationId = conversationId,
            SourceMessageId = messageId,
            PolicyVersionId = state.PolicyVersionId,
            ExpectedConversationVersion = conversation.Version,
            Status = LiveSupportAITurnStatus.Queued,
            CallbackStatus = LiveSupportAICallbackStatus.NotReady,
            QueuedAt = now,
            Version = 1
        };
        db.LiveSupportAITurns.Add(turn);
        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "LiveSupportAITurnQueued",
            PayloadJson = JsonSerializer.Serialize(new
            {
                schemaVersion = "1",
                turnId = turn.Id,
                conversationId,
                queuedAt = now
            })
        });
        state.LastParticipantActivityAt = now;
        state.Version++;
        LiveSupportAITelemetry.TurnsQueued.Add(1);
    }

    public async Task<LiveSupportAIWorkerClaimDto?> ClaimAsync(Guid turnId, CancellationToken cancellationToken)
    {
        var turn = await db.LiveSupportAITurns.SingleOrDefaultAsync(item => item.Id == turnId, cancellationToken);
        if (turn is null) return null;
        if (turn.Status is LiveSupportAITurnStatus.Completed or LiveSupportAITurnStatus.Failed or LiveSupportAITurnStatus.DiscardedAfterHandoff or LiveSupportAITurnStatus.DiscardedAfterDisable or LiveSupportAITurnStatus.Cancelled)
            return null;
        if (turn.Status == LiveSupportAITurnStatus.Queued)
            turn.Status = LiveSupportAITurnStatus.Processing;
        turn.StartedAt ??= DateTime.UtcNow;
        turn.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return await contextBuilder.BuildAsync(turnId, cancellationToken);
    }

    public async Task<string> CompleteAsync(Guid turnId, LiveSupportAIWorkerCompletionDto request, CancellationToken cancellationToken)
    {
        ValidateCompletion(request);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var turn = await db.LiveSupportAITurns.SingleOrDefaultAsync(item => item.Id == turnId, cancellationToken);
        if (turn is null) return "TURN_NOT_FOUND";
        if (turn.Status is LiveSupportAITurnStatus.Completed or LiveSupportAITurnStatus.DiscardedAfterDisable or LiveSupportAITurnStatus.DiscardedAfterHandoff)
            return turn.DecisionHash == request.DecisionHash ? "REPLAYED" : "IDEMPOTENCY_CONFLICT";

        var conversation = await db.LiveSupportConversations.SingleAsync(item => item.Id == turn.ConversationId, cancellationToken);
        var state = await db.LiveSupportAIConversationStates.SingleOrDefaultAsync(item => item.ConversationId == turn.ConversationId, cancellationToken);
        if (state is null || state.Mode != LiveSupportAIMode.AiActive)
        {
            turn.Status = state?.Mode == LiveSupportAIMode.HumanQueued || state?.Mode == LiveSupportAIMode.HumanAssigned
                ? LiveSupportAITurnStatus.DiscardedAfterHandoff
                : LiveSupportAITurnStatus.DiscardedAfterDisable;
            turn.CallbackStatus = LiveSupportAICallbackStatus.Discarded;
            turn.DecisionHash = request.DecisionHash;
            turn.LastSafeCallbackErrorCode = "AI_NOT_ACTIVE";
            turn.CompletedAt = DateTime.UtcNow;
            turn.Version++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "DISCARDED_AI_NOT_ACTIVE";
        }
        if (turn.ExpectedConversationVersion != request.ExpectedConversationVersion ||
            turn.PolicyVersionId != request.ExpectedPolicyVersionId ||
            conversation.Version != request.ExpectedConversationVersion)
        {
            turn.Status = LiveSupportAITurnStatus.DiscardedAfterHandoff;
            turn.CallbackStatus = LiveSupportAICallbackStatus.Discarded;
            turn.DecisionHash = request.DecisionHash;
            turn.LastSafeCallbackErrorCode = "STATE_VERSION_MISMATCH";
            turn.CompletedAt = DateTime.UtcNow;
            turn.Version++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "DISCARDED_STALE_STATE";
        }

        var policy = await db.LiveSupportAIPolicyVersions.SingleAsync(item => item.Id == turn.PolicyVersionId, cancellationToken);
        turn.Status = LiveSupportAITurnStatus.ProviderCompleted;
        turn.ProviderCompletedAt ??= DateTime.UtcNow;
        turn.DecisionHash = request.DecisionHash;
        turn.CallbackStatus = LiveSupportAICallbackStatus.Pending;
        ApplyProviderMetadata(turn, request);

        LiveSupportMessage? outputMessage = null;
        if (!string.IsNullOrWhiteSpace(request.Decision.MessageAr))
        {
            outputMessage = new LiveSupportMessage
            {
                ConversationId = conversation.Id,
                SenderType = LiveSupportSenderType.AI,
                ClientMessageId = $"ai-{turn.Id:N}",
                Type = LiveSupportMessageType.Text,
                Content = request.Decision.MessageAr.Trim(),
                SentAt = DateTime.UtcNow
            };
            db.LiveSupportMessages.Add(outputMessage);
            conversation.LastMessageAt = outputMessage.SentAt;
            conversation.Version++;
            turn.OutputMessageId = outputMessage.Id;
            await AddEventAsync(conversation.Id, LiveSupportEventType.MessageSent, outputMessage.Id, cancellationToken);
            await AddEventAsync(conversation.Id, LiveSupportEventType.AIReplySent, outputMessage.Id, cancellationToken);
        }

        turn.DecisionType = ParseDecisionType(request.Decision.Type);
        if (request.Decision.Type is "propose_action" or "handoff" or "propose_account_creation" or "request_resolution")
            await CreatePendingDecisionAsync(turn, conversation, policy.PendingActionExpirySeconds, request, cancellationToken);
        else if (request.Decision.Type == "request_verification" &&
                 !await db.LiveSupportAIVerificationSessions.AnyAsync(item => item.ConversationId == conversation.Id && (item.Status == LiveSupportAIVerificationStatus.AwaitingLookup || item.Status == LiveSupportAIVerificationStatus.Challenging), cancellationToken))
        {
            var verification = new LiveSupportAIVerificationSession
            {
                ConversationId = conversation.Id,
                PolicyVersionId = turn.PolicyVersionId,
                LookupKey = "pending",
                LookupValueHash = new string('0', 64),
                SelectedQuestionKeysJson = "[]",
                RequiredCorrect = Math.Max(1, policy.VerificationRequiredCorrect),
                MaxAttempts = Math.Clamp(policy.VerificationMaxAttempts, 1, 10),
                Status = LiveSupportAIVerificationStatus.AwaitingLookup,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                Version = 1
            };
            db.LiveSupportAIVerificationSessions.Add(verification);
            await AddEventAsync(conversation.Id, LiveSupportEventType.AIVerificationStarted, verification.Id, cancellationToken);
        }

        turn.Status = LiveSupportAITurnStatus.Completed;
        turn.CallbackStatus = LiveSupportAICallbackStatus.Delivered;
        turn.CallbackAttemptCount++;
        turn.CompletedAt = DateTime.UtcNow;
        turn.Version++;
        await AddEventAsync(conversation.Id, LiveSupportEventType.AITurnCompleted, turn.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LiveSupportAITelemetry.CallbackOutcomes.Add(1, new KeyValuePair<string, object?>("outcome", "completed"), new KeyValuePair<string, object?>("decision_type", request.Decision.Type));
        LiveSupportAITelemetry.InferenceLatency.Record(request.LatencyMs, new KeyValuePair<string, object?>("provider", request.Provider), new KeyValuePair<string, object?>("model", request.Model));
        return "COMPLETED";
    }

    public async Task<string> FailAsync(Guid turnId, LiveSupportAIWorkerFailureDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FailureCode) || request.FailureCode.Length > 100 || request.CallbackIdempotencyKey.Length > 100)
            return "INVALID_FAILURE";
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var turn = await db.LiveSupportAITurns.SingleOrDefaultAsync(item => item.Id == turnId, cancellationToken);
        if (turn is null) return "TURN_NOT_FOUND";
        if (turn.Status is LiveSupportAITurnStatus.Completed or LiveSupportAITurnStatus.Failed or LiveSupportAITurnStatus.DiscardedAfterHandoff)
            return "REPLAYED";

        var conversation = await db.LiveSupportConversations.SingleAsync(item => item.Id == turn.ConversationId, cancellationToken);
        var state = await db.LiveSupportAIConversationStates.SingleOrDefaultAsync(item => item.ConversationId == turn.ConversationId, cancellationToken);
        turn.Status = LiveSupportAITurnStatus.Failed;
        turn.CallbackStatus = LiveSupportAICallbackStatus.Delivered;
        turn.CallbackAttemptCount++;
        turn.FailureCode = request.FailureCode;
        turn.Provider = request.Provider;
        turn.Model = request.Model;
        turn.LatencyMs = request.LatencyMs;
        turn.CompletedAt = DateTime.UtcNow;
        turn.Version++;

        if (state?.Mode == LiveSupportAIMode.AiActive)
        {
            var now = DateTime.UtcNow;
            state.Mode = LiveSupportAIMode.HumanQueued;
            state.HandoffReasonCode = "AI_TURN_FAILED";
            state.HandoffSafeSummary = "تعذر على المساعد إكمال الطلب وتم تحويله للدعم البشري.";
            state.HandedOffAt = now;
            state.Version++;
            if (!await db.LiveSupportQueueEntries.AnyAsync(item => item.ConversationId == conversation.Id && item.DequeuedAt == null, cancellationToken))
                db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversation.Id, EnteredAt = now, Sequence = now.Ticks });
            conversation.Status = LiveSupportConversationStatus.Waiting;
            conversation.QueuedAt ??= now;
            conversation.Version++;
            await AddEventAsync(conversation.Id, LiveSupportEventType.AIHandoffCompleted, turn.Id, cancellationToken);
            await AddEventAsync(conversation.Id, LiveSupportEventType.QueueEntered, null, cancellationToken);
        }
        await AddEventAsync(conversation.Id, LiveSupportEventType.AITurnFailed, turn.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LiveSupportAITelemetry.CallbackOutcomes.Add(1, new KeyValuePair<string, object?>("outcome", "failed"), new KeyValuePair<string, object?>("failure_code", request.FailureCode));
        return "FAILED_AND_HANDED_OFF";
    }

    private static void ValidateCompletion(LiveSupportAIWorkerCompletionDto request)
    {
        if (request.SchemaVersion != "1" || request.Decision.SchemaVersion != "1" ||
            request.DecisionHash.Length != 64 || !request.DecisionHash.All(Uri.IsHexDigit) ||
            request.CallbackIdempotencyKey.Length is < 1 or > 100 || request.LatencyMs < 0 ||
            request.Decision.MessageAr?.Length > LiveSupportAIContractLimits.MaxMessageLength)
            throw new InvalidOperationException("DECISION_SCHEMA_INVALID");
        var computedHash = ComputeDecisionHash(request.Decision);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computedHash), Encoding.ASCII.GetBytes(request.DecisionHash.ToLowerInvariant())))
            throw new InvalidOperationException("DECISION_HASH_INVALID");
        _ = ParseDecisionType(request.Decision.Type);
        var branches = new[] { request.Decision.Action, request.Decision.Verification, request.Decision.AccountCreation, request.Decision.Resolution, request.Decision.Handoff }.Count(item => item.HasValue);
        if ((request.Decision.Type == "reply" && (branches != 0 || string.IsNullOrWhiteSpace(request.Decision.MessageAr))) ||
            (request.Decision.Type != "reply" && branches != 1))
            throw new InvalidOperationException("DECISION_SCHEMA_INVALID");
        var correctBranch = request.Decision.Type switch
        {
            "reply" => true,
            "propose_action" => request.Decision.Action.HasValue,
            "request_verification" => request.Decision.Verification.HasValue,
            "propose_account_creation" => request.Decision.AccountCreation.HasValue,
            "request_resolution" => request.Decision.Resolution.HasValue,
            "handoff" => request.Decision.Handoff.HasValue,
            _ => false
        };
        if (!correctBranch) throw new InvalidOperationException("DECISION_SCHEMA_INVALID");
        if (request.Decision.Type == "handoff" && request.Decision.Handoff!.Value.TryGetProperty("forced", out var forced) && forced.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException("DECISION_SCHEMA_INVALID");
    }

    public static string ComputeDecisionHash(LiveSupportAIWorkerDecisionDto decision)
    {
        var source = JsonSerializer.SerializeToElement(decision, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteCanonical(writer, source);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static LiveSupportAIDecisionType ParseDecisionType(string type) => type switch
    {
        "reply" => LiveSupportAIDecisionType.Reply,
        "propose_action" => LiveSupportAIDecisionType.ProposeAction,
        "request_verification" => LiveSupportAIDecisionType.RequestVerification,
        "propose_account_creation" => LiveSupportAIDecisionType.ProposeAccountCreation,
        "request_resolution" => LiveSupportAIDecisionType.RequestResolution,
        "handoff" => LiveSupportAIDecisionType.Handoff,
        _ => throw new InvalidOperationException("DECISION_SCHEMA_INVALID")
    };

    private static void ApplyProviderMetadata(LiveSupportAITurn turn, LiveSupportAIWorkerCompletionDto request)
    {
        turn.Provider = request.Provider[..Math.Min(request.Provider.Length, 100)];
        turn.Model = request.Model[..Math.Min(request.Model.Length, 150)];
        turn.ProviderResponseId = request.ProviderResponseId?[..Math.Min(request.ProviderResponseId.Length, 200)];
        turn.InputTokenCount = request.InputTokenCount;
        turn.OutputTokenCount = request.OutputTokenCount;
        turn.LatencyMs = request.LatencyMs;
    }

    private async Task CreatePendingDecisionAsync(LiveSupportAITurn turn, LiveSupportConversation conversation, int configuredExpirySeconds, LiveSupportAIWorkerCompletionDto request, CancellationToken cancellationToken)
    {
        var kind = request.Decision.Type switch
        {
            "propose_action" => LiveSupportAIPendingDecisionKind.Action,
            "handoff" => LiveSupportAIPendingDecisionKind.Handoff,
            "propose_account_creation" => LiveSupportAIPendingDecisionKind.AccountCreation,
            "request_resolution" => LiveSupportAIPendingDecisionKind.Resolution,
            _ => throw new InvalidOperationException("DECISION_SCHEMA_INVALID")
        };
        var branch = request.Decision.Action ?? request.Decision.Verification ?? request.Decision.AccountCreation ?? request.Decision.Resolution ?? request.Decision.Handoff!.Value;
        var actionKey = kind == LiveSupportAIPendingDecisionKind.Action && request.Decision.Type == "propose_action"
            ? branch.GetProperty("key").GetString() ?? string.Empty
            : request.Decision.Type switch
            {
                "handoff" => "system.handoff",
                "propose_account_creation" => "system.registration",
                "request_resolution" => "system.resolution",
                _ => $"system.{request.Decision.Type}"
            };
        if (kind == LiveSupportAIPendingDecisionKind.Action && request.Decision.Type == "propose_action")
        {
            if (!conversation.LinkedStudentUserId.HasValue) throw new InvalidOperationException("ACTION_REQUIRES_LINKED_STUDENT");
            var allowed = JsonSerializer.Deserialize<string[]>((await db.LiveSupportAIPolicyVersions.SingleAsync(item => item.Id == turn.PolicyVersionId, cancellationToken)).ActionKeysJson) ?? [];
            if (!allowed.Contains(actionKey, StringComparer.Ordinal)) throw new InvalidOperationException("ACTION_NOT_ALLOWED");
        }
        var protectedJson = branch.GetRawText();
        var protectedBytes = Encoding.UTF8.GetBytes(protectedJson);
        var digest = dataProtector?.ComputeKeyedDigest("pending-decision", protectedBytes)
            ?? Convert.ToHexString(SHA256.HashData(protectedBytes)).ToLowerInvariant();
        var safeJson = request.Decision.Type == "propose_action"
            ? JsonSerializer.Serialize(new
            {
                key = actionKey,
                safeEffectSummaryAr = branch.GetProperty("safeEffectSummaryAr").GetString(),
                safeConsequenceAr = branch.TryGetProperty("safeConsequenceAr", out var consequence) ? consequence.GetString() : null
            })
            : protectedJson;
        db.LiveSupportAIPendingActions.Add(new LiveSupportAIPendingAction
        {
            ConversationId = conversation.Id,
            TurnId = turn.Id,
            DecisionKind = kind,
            StudentUserId = kind == LiveSupportAIPendingDecisionKind.Action ? conversation.LinkedStudentUserId : null,
            PolicyVersionId = turn.PolicyVersionId,
            ActionKey = actionKey,
            SafeProposalJson = safeJson,
            EncryptedPayload = request.Decision.Type == "propose_action"
                ? (dataProtector ?? throw new InvalidOperationException("AI_DATA_PROTECTOR_UNAVAILABLE")).Protect(protectedBytes)
                : null,
            PayloadHash = digest,
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            ConfirmationNonceHash = string.Empty,
            CallbackDecisionHash = request.DecisionHash,
            IdempotencyKey = turn.Id,
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddSeconds(Math.Clamp(configuredExpirySeconds, 60, 3600)),
            Version = 1
        });
        await AddEventAsync(conversation.Id,
            request.Decision.Type == "handoff" ? LiveSupportEventType.AIHandoffRequested : LiveSupportEventType.AIActionProposed,
            turn.Id, cancellationToken);
    }

    private async Task AddEventAsync(Guid conversationId, LiveSupportEventType type, Guid? relatedId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lastSequence = await db.LiveSupportEvents.Where(item => item.ConversationId == conversationId)
            .Select(item => (long?)item.Sequence).MaxAsync(cancellationToken) ?? 0;
        var sequence = Math.Max(lastSequence + 1, now.Ticks);
        var eventId = Guid.NewGuid();
        db.LiveSupportEvents.Add(new LiveSupportEvent { Id = eventId, ConversationId = conversationId, Type = type, RelatedEntityId = relatedId, OccurredAt = now, Sequence = sequence });
        var payload = JsonSerializer.Serialize(new { eventId, conversationId, sequence, occurredAt = now, type = type.ToString(), payload = new { relatedId } });
        db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Conversation:{conversationId:N}", PayloadJson = payload });
        db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = "LiveSupport:Admins", PayloadJson = payload });
    }
}
