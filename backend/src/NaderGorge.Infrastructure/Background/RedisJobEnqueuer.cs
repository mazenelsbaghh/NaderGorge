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

        // Map queue/job names to structured jobType
        var jobType = queueName switch
        {
            "ai-video-queue" => "video analysis",
            "ai-mindmaps-queue" => "mind maps",
            "bullmq-bridge-ingest" or "ai-essay-queue" => "essay",
            "notifications" => "notification",
            _ => "notification"
        };

        // Determine stable jobId
        var jobId = Guid.NewGuid().ToString();
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("lessonVideoId", out var lvProp) || root.TryGetProperty("LessonVideoId", out lvProp))
            {
                jobId = lvProp.GetString() ?? jobId;
            }
            else if (root.TryGetProperty("essaySubmissionId", out var esProp) || root.TryGetProperty("EssaySubmissionId", out esProp))
            {
                jobId = esProp.GetString() ?? jobId;
            }
            else if (root.TryGetProperty("chapterId", out var cProp) || root.TryGetProperty("ChapterId", out cProp))
            {
                jobId = cProp.GetString() ?? jobId;
            }
            else if (root.TryGetProperty("warningId", out var wProp) || root.TryGetProperty("WarningId", out wProp))
            {
                jobId = wProp.GetString() ?? jobId;
            }
        }
        catch { }

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
}
