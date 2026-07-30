using System.Text.Json;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIOutboxDispatchTests
{
    [Fact]
    public async Task Valid_turn_event_dispatches_once_to_the_dedicated_queue()
    {
        var enqueuer = new RecordingJobEnqueuer();
        var turnId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var value = NewEvent(turnId, conversationId);

        await LiveSupportAIOutboxQueueDispatcher.DispatchAsync(value, enqueuer);

        var dispatch = Assert.Single(enqueuer.Dispatches);
        Assert.Equal("ai-live-support-turns", dispatch.QueueName);
        Assert.Equal("respond", dispatch.JobName);
        Assert.Contains(turnId.ToString(), dispatch.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Null(value.TargetGroup);
    }

    [Fact]
    public async Task Redis_failure_is_observable_and_does_not_mark_the_event_processed()
    {
        var value = NewEvent(Guid.NewGuid(), Guid.NewGuid());
        var enqueuer = new RecordingJobEnqueuer { Failure = new InvalidOperationException("redis unavailable") };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LiveSupportAIOutboxQueueDispatcher.DispatchAsync(value, enqueuer));

        Assert.Null(value.ProcessedAt);
        Assert.False(value.IsDeadLetter);
    }

    [Fact]
    public async Task Invalid_or_signalr_shaped_queue_event_is_rejected()
    {
        var value = new OutboxEvent
        {
            Type = LiveSupportAIOutboxQueueDispatcher.EventType,
            TargetGroup = "LiveSupport:Admins",
            PayloadJson = "{}"
        };

        Assert.True(LiveSupportAIOutboxQueueDispatcher.IsTurnQueueEvent(value));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LiveSupportAIOutboxQueueDispatcher.DispatchAsync(value, new RecordingJobEnqueuer()));
    }

    [Fact]
    public async Task Compatible_replay_uses_the_same_deterministic_turn_payload()
    {
        var enqueuer = new RecordingJobEnqueuer();
        var value = NewEvent(Guid.NewGuid(), Guid.NewGuid());

        await LiveSupportAIOutboxQueueDispatcher.DispatchAsync(value, enqueuer);
        await LiveSupportAIOutboxQueueDispatcher.DispatchAsync(value, enqueuer);

        Assert.Equal(2, enqueuer.Dispatches.Count);
        Assert.Equal(enqueuer.Dispatches[0].PayloadJson, enqueuer.Dispatches[1].PayloadJson);
    }

    [Fact]
    public void Fifth_dispatch_failure_marks_dead_letter_without_storing_exception_message()
    {
        var value = NewEvent(Guid.NewGuid(), Guid.NewGuid());
        var exception = new InvalidOperationException("secret-value-must-not-be-stored");

        for (var attempt = 0; attempt < 5; attempt++)
            OutboxProcessorBackgroundService.RecordDispatchFailure(value, exception, DateTime.UtcNow);

        Assert.Equal(5, value.RetryCount);
        Assert.True(value.IsDeadLetter);
        Assert.DoesNotContain("secret-value", value.LastError, StringComparison.Ordinal);
        Assert.Equal("InvalidOperationException: OUTBOX_DISPATCH_FAILED", value.LastError);
    }

    private static OutboxEvent NewEvent(Guid turnId, Guid conversationId) => new()
    {
        Type = LiveSupportAIOutboxQueueDispatcher.EventType,
        TargetGroup = null,
        PayloadJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "1",
            turnId,
            conversationId,
            queuedAt = DateTime.UtcNow
        })
    };

    private sealed class RecordingJobEnqueuer : IJobEnqueuer
    {
        public Exception? Failure { get; init; }
        public List<Dispatch> Dispatches { get; } = [];

        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            if (Failure is not null) throw Failure;
            Dispatches.Add(new Dispatch(queueName, jobName, JsonSerializer.Serialize(data)));
            return Task.CompletedTask;
        }
    }

    private sealed record Dispatch(string QueueName, string JobName, string PayloadJson);
}
