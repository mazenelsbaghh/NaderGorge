using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Features.Exams;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Background;

namespace NaderGorge.Application.Tests;

public sealed class ExamSubmissionOutboxTests
{
    [Fact]
    public async Task ParentQueueOutage_PropagatesForRetryAndRedeliveryKeepsOneJobIdentity()
    {
        var studentId = Guid.NewGuid();
        var submissionEvent = new OutboxEvent
        {
            Type = ExamSubmissionEffects.ParentPushEventType,
            TargetUserId = studentId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new ExamParentPushPayload(studentId, "امتحان شامل"))
        };
        var queue = new RecoveringQueue();
        await using var services = new ServiceCollection().AddSingleton<IJobEnqueuer>(queue).BuildServiceProvider();

        await Assert.ThrowsAsync<IOException>(() => ExamSubmissionOutboxDispatcher.DispatchAsync(submissionEvent, services, default));
        await ExamSubmissionOutboxDispatcher.DispatchAsync(submissionEvent, services, default);

        Assert.Equal(2, queue.Attempts.Count);
        Assert.All(queue.Attempts, attempt => Assert.Equal(submissionEvent.Id.ToString(),
            RedisJobEnqueuer.ResolveStableJobId("notifications", attempt)));
        using var delivered = JsonDocument.Parse(queue.Attempts.Last());
        Assert.Equal(studentId, delivered.RootElement.GetProperty("StudentId").GetGuid());
        Assert.Contains("امتحان شامل", delivered.RootElement.GetProperty("Body").GetString());
    }

    private sealed class RecoveringQueue : IJobEnqueuer
    {
        public List<string> Attempts { get; } = [];
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            Attempts.Add(JsonSerializer.Serialize(data));
            return Attempts.Count == 1 ? Task.FromException(new IOException("Queue temporarily unavailable")) : Task.CompletedTask;
        }
    }
}
