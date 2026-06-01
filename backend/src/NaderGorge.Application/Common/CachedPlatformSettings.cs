using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Common;

public sealed record CachedPlatformSettings(
    int VideoWatchThresholdPercentage,
    int MaxExtraWatchRequestsPerVideo,
    decimal HintPenaltyPercentage
)
{
    public static CachedPlatformSettings Default { get; } = new(30, 3, 25m);
}

public interface ICachedPlatformSettingsReader
{
    Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken);
    void Invalidate();
}

public sealed class CachedPlatformSettingsReader : ICachedPlatformSettingsReader
{
    private const string CacheKey = "platform-settings:v1";

    private readonly IMemoryCache _memoryCache;
    private readonly IAppDbContext _dbContext;

    public CachedPlatformSettingsReader(IMemoryCache memoryCache, IAppDbContext dbContext)
    {
        _memoryCache = memoryCache;
        _dbContext = dbContext;
    }

    public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken)
    {
        return _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            var settings = await _dbContext.PlatformSettings
                .AsNoTracking()
                .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

            return new CachedPlatformSettings(
                GetInt(settings, PlatformSettingKeys.VideoWatchThresholdPercentage, CachedPlatformSettings.Default.VideoWatchThresholdPercentage, minValue: 1),
                GetInt(settings, PlatformSettingKeys.MaxExtraWatchRequestsPerVideo, CachedPlatformSettings.Default.MaxExtraWatchRequestsPerVideo, minValue: 1),
                GetDecimal(settings, PlatformSettingKeys.HintPenaltyPercentage, CachedPlatformSettings.Default.HintPenaltyPercentage, minValue: 0m)
            );
        })!;
    }

    public void Invalidate()
    {
        _memoryCache.Remove(CacheKey);
    }

    private static int GetInt(IReadOnlyDictionary<string, string> settings, string key, int fallback, int minValue)
    {
        return settings.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed) && parsed >= minValue
            ? parsed
            : fallback;
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, string> settings, string key, decimal fallback, decimal minValue)
    {
        return settings.TryGetValue(key, out var rawValue) && decimal.TryParse(rawValue, out var parsed) && parsed >= minValue
            ? parsed
            : fallback;
    }
}
