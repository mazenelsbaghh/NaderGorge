using Microsoft.AspNetCore.SignalR;
using NaderGorge.API.Hubs;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Realtime.Services;
using NaderGorge.Infrastructure.Background;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;

namespace NaderGorge.API.BackgroundServices;

public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PlatformHub> _hubContext;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly string _nodeId;
    private readonly string _releaseId;
    private readonly string _workerId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private readonly TimeSpan _claimLease;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<PlatformHub> hubContext,
        ILogger<OutboxProcessorBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _nodeId = configuration["Cluster:NodeId"] ?? "unknown";
        _releaseId = configuration["Cluster:ReleaseId"] ?? "unknown";
        var claimLeaseSeconds =
            configuration.GetValue("Outbox:ClaimLeaseSeconds", 120);
        if (claimLeaseSeconds <= 0)
            throw new InvalidOperationException(
                "Outbox claim lease must be greater than zero seconds.");
        _claimLease = TimeSpan.FromSeconds(claimLeaseSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing outbox events.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        _logger.LogInformation("OutboxProcessorBackgroundService stopped.");
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken cancellationToken)
    {
        var events = await ClaimBatchAsync(cancellationToken);
        if (events.Count == 0) return;

        using var dispatchScope = _scopeFactory.CreateScope();
        _logger.LogInformation(
            "Dispatching {Count} claimed outbox events for worker {WorkerId}.",
            events.Count,
            _workerId);

        foreach (var @event in events)
        {
            var dispatchStartedAt = Stopwatch.GetTimestamp();
            var telemetryDimensions =
                RealtimeTelemetry.Dimensions(@event.Type, _nodeId, _releaseId);
            Guid? eventId = null;
            try
            {
                eventId = EnsureStableStaffEventId(@event);
                var retainedLease = await DispatchWithLeaseRenewalAsync(
                    @event,
                    dispatchScope.ServiceProvider,
                    cancellationToken);
                if (!retainedLease)
                {
                    _logger.LogWarning(
                        "Outbox dispatch completed after lease ownership changed. EventId={EventId}",
                        @event.Id);
                    continue;
                }
                var acknowledged = await AcknowledgeAsync(@event, cancellationToken);
                if (!acknowledged)
                {
                    _logger.LogWarning(
                        "Outbox acknowledgement skipped after lease ownership changed. EventId={EventId}",
                        @event.Id);
                    continue;
                }

                RealtimeTelemetry.RecordDispatchSucceeded(
                    telemetryDimensions,
                    Stopwatch.GetElapsedTime(dispatchStartedAt).TotalMilliseconds);
                _logger.LogInformation(
                    "Outbox event dispatched. EventId={EventId} EventType={EventType} Attempt={Attempt} TargetKind={TargetKind}",
                    eventId,
                    @event.Type,
                    @event.RetryCount + 1,
                    GetTargetKind(@event));
            }
            catch (Exception ex)
            {
                RealtimeTelemetry.RecordDispatchFailed(
                    telemetryDimensions,
                    Stopwatch.GetElapsedTime(dispatchStartedAt).TotalMilliseconds);
                _logger.LogError(
                    ex,
                    "Failed to dispatch outbox event. EventId={EventId} EventType={EventType} Attempt={Attempt} TargetKind={TargetKind}",
                    eventId,
                    @event.Type,
                    @event.RetryCount + 1,
                    GetTargetKind(@event));
                RecordDispatchFailure(@event, ex, DateTime.UtcNow);
                var failureRecorded =
                    await RecordFailureAsync(@event, cancellationToken);
                if (!failureRecorded)
                {
                    _logger.LogWarning(
                        "Outbox failure update skipped after lease ownership changed. EventId={EventId}",
                        @event.Id);
                    continue;
                }

                if (@event.IsDeadLetter)
                {
                    RealtimeTelemetry.RecordDeadLetter(telemetryDimensions);
                    _logger.LogCritical(
                        "Outbox event marked as dead letter. EventId={EventId} EventType={EventType} Attempt={Attempt}",
                        eventId,
                        @event.Type,
                        @event.RetryCount);
                }
                else
                {
                    RealtimeTelemetry.RecordRetry(telemetryDimensions);
                }
            }
        }
    }

    private async Task<List<OutboxEvent>> ClaimBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = CreateLeaseStore(scope.ServiceProvider);
        var events = await store.ClaimBatchAsync(_workerId, _claimLease, 1, ct);
        RecordClaims(events);
        return events;
    }

    private void RecordClaims(IEnumerable<OutboxEvent> events)
    {
        foreach (var @event in events)
        {
            var claimedAt = @event.ClaimedAt ?? DateTime.UtcNow;
            var dimensions =
                RealtimeTelemetry.Dimensions(@event.Type, _nodeId, _releaseId);
            RealtimeTelemetry.RecordClaim(
                dimensions,
                (claimedAt - @event.CreatedAt).TotalMilliseconds);
        }
    }

    private async Task DispatchAsync(
        OutboxEvent @event,
        IServiceProvider services,
        CancellationToken ct)
    {
        if (AdminAIOutboxQueueDispatcher.IsTurnQueueEvent(@event))
        {
            var jobEnqueuer = services.GetService<IJobEnqueuer>()
                ?? throw new InvalidOperationException("Admin AI queue dispatcher is unavailable.");
            await AdminAIOutboxQueueDispatcher.DispatchAsync(@event, jobEnqueuer);
        }
        else if (AdminAIOutboxQueueDispatcher.IsRealtimeEvent(@event))
        {
            var envelope = AdminAIOutboxQueueDispatcher.ValidateRealtimeEnvelope(@event);
            await _hubContext.Clients.Group($"User_{@event.TargetUserId}")
                .SendAsync("AdminAIEvent", envelope, ct);
        }
        else if (LiveSupportAIOutboxQueueDispatcher.IsTurnQueueEvent(@event))
        {
            var jobEnqueuer = services.GetService<IJobEnqueuer>()
                ?? throw new InvalidOperationException("Live-support AI queue dispatcher is unavailable.");
            await LiveSupportAIOutboxQueueDispatcher.DispatchAsync(@event, jobEnqueuer);
        }
        else if (EssayEvaluationOutboxQueueDispatcher.IsEssayEvaluationEvent(@event))
        {
            var jobEnqueuer = services.GetService<IJobEnqueuer>()
                ?? throw new InvalidOperationException("Essay evaluation queue dispatcher is unavailable.");
            await EssayEvaluationOutboxQueueDispatcher.DispatchAsync(@event, jobEnqueuer);
        }
        else if (ParentPurchaseOutboxDispatcher.IsPurchaseEvent(@event))
        {
            var jobEnqueuer = services.GetService<IJobEnqueuer>()
                ?? throw new InvalidOperationException("Parent purchase notification dispatcher is unavailable.");
            await ParentPurchaseOutboxDispatcher.DispatchAsync(@event, jobEnqueuer);
            if (!string.IsNullOrEmpty(@event.TargetUserId))
            {
                await _hubContext.Clients.Group($"User_{@event.TargetUserId}")
                    .SendAsync(@event.Type, @event.PayloadJson, ct);
            }
        }
        else if (IsLiveSupportEvent(@event))
        {
            if (!IsAllowedLiveSupportGroup(@event.TargetGroup))
                throw new InvalidOperationException("Rejected unsafe live-support outbox target.");
            if (!IsValidLiveSupportPayload(@event.PayloadJson))
                throw new InvalidOperationException("Rejected malformed live-support event payload.");
            var liveSupportHub =
                services.GetService<IHubContext<LiveSupportHub>>();
            if (liveSupportHub is null)
                throw new InvalidOperationException("Live-support hub dispatcher is unavailable.");
            await liveSupportHub.Clients.Group(@event.TargetGroup!)
                .SendAsync(@event.Type, @event.PayloadJson, ct);
        }
        else if (!string.IsNullOrEmpty(@event.TargetUserId))
        {
            await _hubContext.Clients.Group($"User_{@event.TargetUserId}")
                .SendAsync(@event.Type, @event.PayloadJson, ct);
        }
        else if (!string.IsNullOrEmpty(@event.TargetGroup))
        {
            if (@event.TargetGroup.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                @event.TargetGroup.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                await _hubContext.Clients.All.SendAsync(@event.Type, @event.PayloadJson, ct);
            }
            else
            {
                await _hubContext.Clients.Group(@event.TargetGroup)
                    .SendAsync(@event.Type, @event.PayloadJson, ct);
            }
        }
        else
        {
            _logger.LogWarning(
                "Outbox event {Id} has no target specified. Skipping broadcast to prevent unauthorized leak.",
                @event.Id);
        }
    }

    private async Task<bool> DispatchWithLeaseRenewalAsync(
        OutboxEvent @event,
        IServiceProvider services,
        CancellationToken stoppingToken)
    {
        using var dispatchCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewal = MaintainLeaseAsync(@event.Id, dispatchCancellation);
        try
        {
            await DispatchAsync(
                @event,
                services,
                dispatchCancellation.Token);
        }
        finally
        {
            await dispatchCancellation.CancelAsync();
        }

        return await renewal;
    }

    private async Task<bool> MaintainLeaseAsync(
        Guid eventId,
        CancellationTokenSource dispatchCancellation)
    {
        using var timer = new PeriodicTimer(_claimLease / 3);
        try
        {
            while (await timer.WaitForNextTickAsync(
                dispatchCancellation.Token))
            {
                using var scope = _scopeFactory.CreateScope();
                var renewed = await CreateLeaseStore(scope.ServiceProvider)
                    .TryRenewLeaseAsync(
                        eventId,
                        _workerId,
                        _claimLease,
                        dispatchCancellation.Token);
                if (renewed) continue;
                await dispatchCancellation.CancelAsync();
                return false;
            }
        }
        catch (OperationCanceledException)
            when (dispatchCancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            await dispatchCancellation.CancelAsync();
            throw;
        }

        return true;
    }

    private async Task<bool> AcknowledgeAsync(OutboxEvent @event, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = CreateLeaseStore(scope.ServiceProvider);
        return await store.TryAcknowledgeAsync(
            @event.Id,
            _workerId,
            @event.PayloadJson,
            ct);
    }

    private async Task<bool> RecordFailureAsync(
        OutboxEvent @event,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = CreateLeaseStore(scope.ServiceProvider);
        return await store.TryRecordFailureAsync(@event, _workerId, ct);
    }

    private static OutboxLeaseStore CreateLeaseStore(IServiceProvider services) =>
        new(services.GetRequiredService<IAppDbContext>());

    internal static bool IsLiveSupportEvent(OutboxEvent value) => !LiveSupportAIOutboxQueueDispatcher.IsTurnQueueEvent(value) && (value.Type.StartsWith("LiveSupport", StringComparison.Ordinal) || value.TargetGroup?.StartsWith("LiveSupport:", StringComparison.Ordinal) == true);
    internal static bool IsAllowedLiveSupportGroup(string? group) => group == "LiveSupport:Admins" || group == "LiveSupport:Queue" || group?.StartsWith("LiveSupport:Conversation:", StringComparison.Ordinal) == true || group?.StartsWith("LiveSupport:Participant:", StringComparison.Ordinal) == true || group?.StartsWith("LiveSupport:Staff:", StringComparison.Ordinal) == true;

    /// <summary>
    /// Validates the durable envelope before it reaches SignalR. A missing event
    /// id makes client deduplication impossible, while a malformed sequence must
    /// not poison reconnect recovery. Sequence gaps are deliberately allowed:
    /// clients recover them from the authoritative snapshot/event page.
    /// </summary>
    public static bool IsValidLiveSupportPayload(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("eventId", out var eventId) ||
                eventId.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(eventId.GetString(), out var parsedEventId) ||
                parsedEventId == Guid.Empty)
                return false;

            if (root.TryGetProperty("sequence", out var sequence) &&
                (sequence.ValueKind != JsonValueKind.Number || !sequence.TryGetInt64(out var value) || value <= 0))
                return false;

            return root.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(type.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Older producers may have created a StaffDataChanged row without an event ID.
    /// Backfill it once from the durable outbox ID; retries and dead-letter processing
    /// then reuse the same payload and cannot create a second logical event.
    /// </summary>
    internal static Guid? EnsureStableStaffEventId(OutboxEvent value)
    {
        if (!string.Equals(value.Type, "StaffDataChanged", StringComparison.Ordinal))
            return null;

        if (JsonNode.Parse(value.PayloadJson) is not JsonObject payload)
            return null;

        if (Guid.TryParse(payload["eventId"]?.GetValue<string>(), out var eventId) && eventId != Guid.Empty)
            return eventId;

        var stableEventId = value.Id == Guid.Empty ? Guid.NewGuid() : value.Id;
        payload["eventId"] = stableEventId.ToString();
        value.PayloadJson = payload.ToJsonString();
        return stableEventId;
    }

    private static string GetTargetKind(OutboxEvent value) =>
        !string.IsNullOrWhiteSpace(value.TargetUserId) ? "user" :
        !string.IsNullOrWhiteSpace(value.TargetGroup) ? "group" : "none";

    public static void RecordDispatchFailure(OutboxEvent value, Exception exception, DateTime utcNow)
    {
        value.RetryCount++;
        value.UpdatedAt = utcNow;
        value.LastError = $"{exception.GetType().Name}: OUTBOX_DISPATCH_FAILED";
        value.IsDeadLetter = value.RetryCount >= 5;
        value.NextAttemptAt = value.IsDeadLetter
            ? null
            : utcNow.AddSeconds(Math.Pow(2, value.RetryCount) * 5);
        value.ClaimedBy = null;
        value.ClaimedAt = null;
        value.LeaseExpiresAt = null;
    }
}

public static class ParentPurchaseOutboxDispatcher
{
    public static bool IsPurchaseEvent(OutboxEvent value) =>
        string.Equals(value.Type, "CodeActivated", StringComparison.Ordinal) ||
        string.Equals(value.Type, "PackageAccessGranted", StringComparison.Ordinal);

    public static async Task DispatchAsync(OutboxEvent value, IJobEnqueuer jobEnqueuer)
    {
        var studentId = value.TargetUserId;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            using var document = JsonDocument.Parse(value.PayloadJson);
            if (document.RootElement.TryGetProperty("userId", out var userIdProperty))
            {
                studentId = userIdProperty.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(studentId))
            throw new InvalidOperationException("Parent purchase notification requires a student user id.");

        await jobEnqueuer.EnqueueJobAsync("notifications", "parent-push", new
        {
            outboxEventId = value.Id,
            StudentId = studentId,
            Title = "شراء جديد للطالب",
            Body = "تم تفعيل محتوى جديد للطالب.",
            Category = "Purchase",
            ParentPush = true
        });
    }
}

public static class EssayEvaluationOutboxQueueDispatcher
{
    public const string EventType = "EssayEvaluationQueued";
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsEssayEvaluationEvent(OutboxEvent value) =>
        string.Equals(value.Type, EventType, StringComparison.Ordinal);

    public static async Task DispatchAsync(OutboxEvent value, IJobEnqueuer jobEnqueuer)
    {
        var payload = JsonSerializer.Deserialize<EssayEvaluationQueuePayload>(value.PayloadJson, PayloadJsonOptions)
            ?? throw new InvalidOperationException("Essay evaluation outbox payload is empty.");

        if (payload.EssaySubmissionId == Guid.Empty || payload.QuestionId == Guid.Empty || payload.StudentId == Guid.Empty)
            throw new InvalidOperationException("Essay evaluation outbox payload is invalid.");

        await jobEnqueuer.EnqueueJobAsync("bullmq-bridge-ingest", "evaluateEssay", new
        {
            essaySubmissionId = payload.EssaySubmissionId,
            questionId = payload.QuestionId,
            studentId = payload.StudentId,
            questionText = payload.QuestionText,
            answerText = payload.AnswerText,
            expectedAnswer = payload.ExpectedAnswer
        });
    }

    public sealed record EssayEvaluationQueuePayload(
        Guid EssaySubmissionId,
        Guid QuestionId,
        Guid StudentId,
        string QuestionText,
        string AnswerText,
        string ExpectedAnswer);
}

public static class LiveSupportAIOutboxQueueDispatcher
{
    public const string EventType = "LiveSupportAITurnQueued";
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsTurnQueueEvent(OutboxEvent value) =>
        string.Equals(value.Type, EventType, StringComparison.Ordinal);

    public static async Task DispatchAsync(OutboxEvent value, IJobEnqueuer jobEnqueuer)
    {
        var payload = JsonSerializer.Deserialize<LiveSupportAITurnQueuePayload>(value.PayloadJson, PayloadJsonOptions)
            ?? throw new InvalidOperationException("Live-support turn outbox payload is empty.");

        if (payload.SchemaVersion != "1" || payload.TurnId == Guid.Empty || payload.ConversationId == Guid.Empty)
            throw new InvalidOperationException("Live-support turn outbox payload is invalid.");

        await jobEnqueuer.EnqueueJobAsync(
            "ai-live-support-turns",
            "respond",
            new
            {
                schemaVersion = payload.SchemaVersion,
                turnId = payload.TurnId,
                conversationId = payload.ConversationId,
                queuedAt = payload.QueuedAt
            });
    }

    public sealed record LiveSupportAITurnQueuePayload(
        string SchemaVersion,
        Guid TurnId,
        Guid ConversationId,
        DateTime QueuedAt);
}

public static class AdminAIOutboxQueueDispatcher
{
    public const string QueueEventType = "AdminAITurnQueued";
    public const string RealtimeEventType = "AdminAIRealtime";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static bool IsTurnQueueEvent(OutboxEvent value) => string.Equals(value.Type, QueueEventType, StringComparison.Ordinal);
    public static bool IsRealtimeEvent(OutboxEvent value) => string.Equals(value.Type, RealtimeEventType, StringComparison.Ordinal);

    public static async Task DispatchAsync(OutboxEvent value, IJobEnqueuer jobs)
    {
        var payload = JsonSerializer.Deserialize<TurnQueuePayload>(value.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Admin AI queue payload is empty.");
        if (payload.SchemaVersion != "1" || payload.TurnId == Guid.Empty || payload.ConversationId == Guid.Empty)
            throw new InvalidOperationException("Admin AI queue payload is invalid.");
        await jobs.EnqueueJobAsync("ai-admin-agent-turns", "respond", new
        {
            schemaVersion = payload.SchemaVersion,
            turnId = payload.TurnId,
            conversationId = payload.ConversationId,
            queuedAt = payload.QueuedAt
        });
    }

    public static object ValidateRealtimeEnvelope(OutboxEvent value)
    {
        if (string.IsNullOrWhiteSpace(value.TargetUserId) || value.TargetGroup is not null)
            throw new InvalidOperationException("Admin AI realtime events must target exactly one owner.");
        var payload = JsonSerializer.Deserialize<RealtimePayload>(value.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Admin AI realtime payload is empty.");
        if (payload.SchemaVersion != "1" || payload.EventId == Guid.Empty || payload.ConversationId == Guid.Empty ||
            payload.Sequence < 1 || payload.Type is not ("snapshot_changed" or "access_revoked"))
            throw new InvalidOperationException("Admin AI realtime envelope is invalid.");
        return payload;
    }

    public sealed record TurnQueuePayload(string SchemaVersion, Guid TurnId, Guid ConversationId, DateTime QueuedAt);
    public sealed record RealtimePayload(string SchemaVersion, Guid EventId, Guid ConversationId, Guid? TurnId, Guid? ProposalId, long Sequence, string Type, DateTime OccurredAt);
}
