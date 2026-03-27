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
        var payload = JsonSerializer.Serialize(new
        {
            name = jobName,
            data = data,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        // Simple List Push. A lightweight Node worker can BRPOP this 
        // to pass the job cleanly to native BullMQ.
        await db.ListLeftPushAsync(queueName, payload);
    }
}
