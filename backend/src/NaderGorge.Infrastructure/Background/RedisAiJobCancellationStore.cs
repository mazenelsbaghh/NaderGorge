using NaderGorge.Application.Interfaces;
using StackExchange.Redis;

namespace NaderGorge.Infrastructure.Background;

public sealed class RedisAiJobCancellationStore(IConnectionMultiplexer redis) : IAiJobCancellationStore
{
    private static readonly TimeSpan CancellationLifetime = TimeSpan.FromHours(24);
    private readonly IDatabase _database = redis.GetDatabase();

    public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => RequestAsync(videoId.ToString());

    public Task RequestMindmapCancellationAsync(Guid videoId) => RequestAsync(MindmapJobId(videoId));

    public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => ClearAsync(videoId.ToString());

    public Task ClearMindmapCancellationAsync(Guid videoId) => ClearAsync(MindmapJobId(videoId));

    private async Task RequestAsync(string jobId) =>
        await _database.StringSetAsync(CancellationKey(jobId), "1", CancellationLifetime);

    private async Task ClearAsync(string jobId) => await _database.KeyDeleteAsync(CancellationKey(jobId));

    private static string MindmapJobId(Guid videoId) => $"{videoId}_mindmaps";

    private static string CancellationKey(string jobId) => $"cancelled-jobs:{jobId}";
}
