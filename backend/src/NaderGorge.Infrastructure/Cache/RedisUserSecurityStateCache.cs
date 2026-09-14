using System.Text.Json;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Interfaces;
using StackExchange.Redis;

namespace NaderGorge.Infrastructure.Cache;

public sealed class RedisUserSecurityStateCache(
    IRedisConnectionFactory connectionFactory,
    ILogger<RedisUserSecurityStateCache> logger) : IUserSecurityStateCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
    private readonly IRedisConnectionFactory _connectionFactory = connectionFactory;
    private readonly ILogger<RedisUserSecurityStateCache> _logger = logger;

    public async Task<UserSecurityStateCacheLookup> GetAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var cachedJson = await _connectionFactory.GetDatabase()
                .StringGetAsync(Key(userId))
                .WaitAsync(ct);
            if (!cachedJson.HasValue)
            {
                return new(UserSecurityStateCacheStatus.Miss, null);
            }

            var state = JsonSerializer.Deserialize<UserSecurityState>(
                cachedJson.ToString());
            return state is null
                ? new(UserSecurityStateCacheStatus.Miss, null)
                : new(UserSecurityStateCacheStatus.Hit, state);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is RedisException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "User security cache read unavailable for user {UserId}",
                userId);
            return new(UserSecurityStateCacheStatus.Unavailable, null);
        }
    }

    public async Task SetAsync(Guid userId, UserSecurityState state, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            await _connectionFactory.GetDatabase()
                .StringSetAsync(Key(userId), JsonSerializer.Serialize(state), Lifetime)
                .WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(
                exception,
                "User security cache write unavailable for user {UserId}",
                userId);
        }
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            await _connectionFactory.GetDatabase()
                .KeyDeleteAsync(Key(userId))
                .WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(
                exception,
                "User security cache invalidation unavailable for user {UserId}",
                userId);
        }
    }

    private static string Key(Guid userId) => $"auth:security-state:v1:{userId:N}";
}
