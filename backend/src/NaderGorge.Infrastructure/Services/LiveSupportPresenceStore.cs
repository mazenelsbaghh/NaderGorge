using NaderGorge.Application.Features.LiveSupport.Interfaces;
using StackExchange.Redis;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportPresenceStore(IConnectionMultiplexer redis) : ILiveSupportPresenceStore
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string ExpiryKey = "live-support:presence:disconnect-expiry";

    public async Task ConnectedAsync(Guid staffUserId, string connectionId)
    {
        await _db.SetAddAsync(ConnectionsKey(staffUserId), connectionId);
        await _db.KeyExpireAsync(ConnectionsKey(staffUserId), TimeSpan.FromHours(24));
        await _db.SortedSetRemoveAsync(ExpiryKey, staffUserId.ToString("N"));
        await HeartbeatAsync(staffUserId);
    }

    public async Task DisconnectedAsync(Guid staffUserId, string connectionId)
    {
        await _db.SetRemoveAsync(ConnectionsKey(staffUserId), connectionId);
        if (await _db.SetLengthAsync(ConnectionsKey(staffUserId)) == 0)
            await _db.SortedSetAddAsync(ExpiryKey, staffUserId.ToString("N"), DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds());
    }

    public async Task HeartbeatAsync(Guid staffUserId)
    {
        await _db.StringSetAsync($"live-support:presence:last-seen:{staffUserId:N}", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), TimeSpan.FromMinutes(5));
    }

    public async Task<bool> IsConnectedAsync(Guid staffUserId) => await _db.SetLengthAsync(ConnectionsKey(staffUserId)) > 0;

    public async Task<IReadOnlyList<Guid>> ClaimExpiredDisconnectsAsync(DateTime utcNow)
    {
        var values = await _db.SortedSetRangeByScoreAsync(ExpiryKey, stop: new DateTimeOffset(utcNow).ToUnixTimeSeconds());
        var result = new List<Guid>(values.Length);
        foreach (var value in values)
        {
            if (Guid.TryParseExact(value.ToString(), "N", out var id) && await _db.SortedSetRemoveAsync(ExpiryKey, value)) result.Add(id);
        }
        return result;
    }

    private static string ConnectionsKey(Guid id) => $"live-support:presence:connections:{id:N}";
}
