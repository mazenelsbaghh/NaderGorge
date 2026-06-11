using StackExchange.Redis;
using NaderGorge.Infrastructure.Cache;
using System.Text.Json;

namespace NaderGorge.Infrastructure.Services;

public interface IIdempotencyService
{
    Task<bool> TryLockAsync(string key, TimeSpan ttl);
    Task SaveResultAsync(string key, int statusCode, string responseBody, TimeSpan ttl);
    Task<(int StatusCode, string ResponseBody)?> GetResultAsync(string key);
}

public class RedisIdempotencyService : IIdempotencyService
{
    private readonly IDatabase _db;

    public RedisIdempotencyService(IRedisConnectionFactory connectionFactory)
    {
        _db = connectionFactory.GetDatabase();
    }

    public async Task<bool> TryLockAsync(string key, TimeSpan ttl)
    {
        return await _db.StringSetAsync($"lock:{key}", "processing", ttl, When.NotExists);
    }

    public async Task SaveResultAsync(string key, int statusCode, string responseBody, TimeSpan ttl)
    {
        var data = JsonSerializer.Serialize(new IdempotentResponse
        {
            StatusCode = statusCode,
            ResponseBody = responseBody
        });

        var batch = _db.CreateBatch();
        var t1 = batch.StringSetAsync($"result:{key}", data, ttl);
        var t2 = batch.KeyDeleteAsync($"lock:{key}");
        batch.Execute();

        await Task.WhenAll(t1, t2);
    }

    public async Task<(int StatusCode, string ResponseBody)?> GetResultAsync(string key)
    {
        var value = await _db.StringGetAsync($"result:{key}");
        if (value.IsNullOrEmpty) return null;

        var response = JsonSerializer.Deserialize<IdempotentResponse>(value!);
        if (response == null) return null;

        return (response.StatusCode, response.ResponseBody);
    }

    private class IdempotentResponse
    {
        public int StatusCode { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
    }
}
