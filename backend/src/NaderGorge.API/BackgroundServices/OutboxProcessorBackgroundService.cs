using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Hubs;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Realtime.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;

namespace NaderGorge.API.BackgroundServices;

public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PlatformHub> _hubContext;
    private readonly IHubContext<LiveSupportHub>? _liveSupportHub;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<PlatformHub> hubContext,
        ILogger<OutboxProcessorBackgroundService> logger,
        IHubContext<LiveSupportHub>? liveSupportHub = null)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _liveSupportHub = liveSupportHub;
        _logger = logger;
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        if (db is not DbContext dbContext)
        {
            _logger.LogError("AppDbContext is not a DbContext instance.");
            return;
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var events = await dbContext.Set<OutboxEvent>()
                .FromSqlRaw("SELECT * FROM outbox_events WHERE \"ProcessedAt\" IS NULL AND \"IsDeadLetter\" = FALSE AND \"RetryCount\" < 5 ORDER BY \"CreatedAt\" LIMIT 50 FOR UPDATE SKIP LOCKED")
                .ToListAsync(cancellationToken);

            if (!events.Any())
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            var filteredEvents = events.Where(e =>
            {
                if (e.RetryCount == 0) return true;
                var lastAttempt = e.UpdatedAt ?? e.CreatedAt;
                var delaySeconds = Math.Pow(2, e.RetryCount) * 5; // 10s, 20s, 40s, 80s
                return (now - lastAttempt).TotalSeconds >= delaySeconds;
            }).ToList();

            if (!filteredEvents.Any())
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            _logger.LogInformation("Processing {Count} outbox events after filtering.", filteredEvents.Count);

            foreach (var @event in filteredEvents)
            {
                var dispatchStartedAt = Stopwatch.GetTimestamp();
                Guid? eventId = null;
                try
                {
                    eventId = EnsureStableStaffEventId(@event);
                    if (LiveSupportAIOutboxQueueDispatcher.IsTurnQueueEvent(@event))
                    {
                        var jobEnqueuer = scope.ServiceProvider.GetService<IJobEnqueuer>();
                        if (jobEnqueuer is null)
                            throw new InvalidOperationException("Live-support AI queue dispatcher is unavailable.");
                        await LiveSupportAIOutboxQueueDispatcher.DispatchAsync(@event, jobEnqueuer);
                    }
                    else if (EssayEvaluationOutboxQueueDispatcher.IsEssayEvaluationEvent(@event))
                    {
                        var jobEnqueuer = scope.ServiceProvider.GetService<IJobEnqueuer>()
                            ?? throw new InvalidOperationException("Essay evaluation queue dispatcher is unavailable.");

                        await EssayEvaluationOutboxQueueDispatcher.DispatchAsync(@event, jobEnqueuer);
                    }
                    else if (ParentPurchaseOutboxDispatcher.IsPurchaseEvent(@event))
                    {
                        var jobEnqueuer = scope.ServiceProvider.GetService<IJobEnqueuer>()
                            ?? throw new InvalidOperationException("Parent purchase notification dispatcher is unavailable.");

                        await ParentPurchaseOutboxDispatcher.DispatchAsync(@event, jobEnqueuer);

                        if (!string.IsNullOrEmpty(@event.TargetUserId))
                        {
                            await _hubContext.Clients.Group($"User_{@event.TargetUserId}")
                                .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                        }
                    }
                    else if (IsLiveSupportEvent(@event))
                    {
                        if (!IsAllowedLiveSupportGroup(@event.TargetGroup))
                            throw new InvalidOperationException("Rejected unsafe live-support outbox target.");
                        if (!IsValidLiveSupportPayload(@event.PayloadJson))
                            throw new InvalidOperationException("Rejected malformed live-support event payload.");
                        if (_liveSupportHub is null) throw new InvalidOperationException("Live-support hub dispatcher is unavailable.");
                        await _liveSupportHub.Clients.Group(@event.TargetGroup!).SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                    }
                    else if (!string.IsNullOrEmpty(@event.TargetUserId))
                    {
                        await _hubContext.Clients.Group($"User_{@event.TargetUserId}")
                            .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                    }
                    else if (!string.IsNullOrEmpty(@event.TargetGroup))
                    {
                        if (@event.TargetGroup.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                            @event.TargetGroup.Equals("All", StringComparison.OrdinalIgnoreCase))
                        {
                            await _hubContext.Clients.All
                                .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                        }
                        else
                        {
                            await _hubContext.Clients.Group(@event.TargetGroup)
                                .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Outbox event {Id} has no target specified. Skipping broadcast to prevent unauthorized leak.", @event.Id);
                    }

                    @event.ProcessedAt = DateTime.UtcNow;
                    RealtimeTelemetry.EventsDispatched.Add(1);
                    RealtimeTelemetry.DispatchLatency.Record(Stopwatch.GetElapsedTime(dispatchStartedAt).TotalMilliseconds);
                    _logger.LogInformation(
                        "Outbox event dispatched. EventId={EventId} EventType={EventType} Attempt={Attempt} TargetKind={TargetKind}",
                        eventId,
                        @event.Type,
                        @event.RetryCount + 1,
                        GetTargetKind(@event));
                }
                catch (Exception ex)
                {
                    RealtimeTelemetry.DispatchFailures.Add(1);
                    RealtimeTelemetry.DispatchLatency.Record(Stopwatch.GetElapsedTime(dispatchStartedAt).TotalMilliseconds);
                    _logger.LogError(
                        ex,
                        "Failed to dispatch outbox event. EventId={EventId} EventType={EventType} Attempt={Attempt} TargetKind={TargetKind}",
                        eventId,
                        @event.Type,
                        @event.RetryCount + 1,
                        GetTargetKind(@event));
                    RecordDispatchFailure(@event, ex, DateTime.UtcNow);

                    if (@event.IsDeadLetter)
                    {
                        RealtimeTelemetry.DeadLetters.Add(1);
                        _logger.LogCritical(
                            "Outbox event marked as dead letter. EventId={EventId} EventType={EventType} Attempt={Attempt}",
                            eventId,
                            @event.Type,
                            @event.RetryCount);
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error occurred processing outbox events; transaction rolled back.");
            throw;
        }
    }

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
