using NaderGorge.Application.Interfaces;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace NaderGorge.Infrastructure.Background;

public class RedisJobEnqueuer : IJobEnqueuer
{
    private readonly IConnectionMultiplexer _redis;

    public RedisJobEnqueuer(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
    {
        var db = _redis.GetDatabase();
        var payloadJson = JsonSerializer.Serialize(data);
        var messageId = Guid.NewGuid().ToString();
        var jobType = ResolveJobType(queueName, jobName);
        var jobId = ResolveStableJobId(queueName, payloadJson);

        var values = new NameValueEntry[]
        {
            new("messageId", messageId),
            new("jobType", jobType),
            new("jobId", jobId),
            new("payload", payloadJson),
            new("createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
        };

        await db.StreamAddAsync("job-stream", values);
    }

    public static string ResolveJobType(string queueName, string jobName) => (queueName, jobName) switch
    {
        ("ai-video-queue", "analyze-chapters") => "video analysis",
        ("ai-mindmaps-queue", "generate-mindmaps" or "regenerate-single-mindmap") => "mind maps",
        ("bullmq-bridge-ingest" or "ai-essay-queue", "evaluateEssay" or "evaluate-essay") => "essay",
        ("notifications", _) => "notification",
        ("ai-live-support-turns", "respond") => "live support turn",
        _ => throw new InvalidOperationException($"Unsupported queue/job mapping: {queueName}/{jobName}.")
    };

    public static string ResolveStableJobId(string queueName, string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        if (queueName == "ai-live-support-turns")
        {
            if (!TryGetString(root, "turnId", "TurnId", out var turnId) || !Guid.TryParse(turnId, out var parsedTurnId))
                throw new InvalidOperationException("Live-support queue payload requires a valid turnId.");
            return $"turn:{parsedTurnId:D}";
        }

        foreach (var pair in new[]
                 {
                     ("lessonVideoId", "LessonVideoId"),
                     ("essaySubmissionId", "EssaySubmissionId"),
                     ("chapterId", "ChapterId"),
                     ("warningId", "WarningId")
                 })
        {
            if (TryGetString(root, pair.Item1, pair.Item2, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return Guid.NewGuid().ToString("D");
    }

    private static bool TryGetString(JsonElement root, string camelCaseName, string pascalCaseName, out string? value)
    {
        if (root.TryGetProperty(camelCaseName, out var property) || root.TryGetProperty(pascalCaseName, out property))
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }
}
