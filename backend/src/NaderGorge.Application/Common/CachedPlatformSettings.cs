using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Common;

public sealed record CachedPlatformSettings(
    int VideoWatchThresholdPercentage,
    int MaxExtraWatchRequestsPerVideo,
    decimal HintPenaltyPercentage,
    string PlatformName,
    string SupportPhoneNumber,
    string SupportWhatsAppUrl,
    string YouTubeChannelUrl,
    string TelegramChannelUrl,
    int MaxActiveDevicesPerStudent,
    bool EnableWatermark,
    decimal WatermarkOpacity,
    bool MaintenanceMode,
    string MaintenanceMessage,
    decimal BunnyStreamStorageRateUsdPerGb,
    decimal BunnyStreamBandwidthRateUsdPerGb,
    bool LiveSupportEnabled,
    decimal PlayerShadowTopOpacity,
    decimal PlayerShadowBottomOpacity,
    int YouTubePlayerShadowHideDelaySeconds,
    int BunnyPlayerShadowHideDelaySeconds
)
{
    public static CachedPlatformSettings Default { get; } = new(
        30,
        3,
        25m,
        "منصة مسار التعليمية",
        "01000000000",
        "https://wa.me/201000000000",
        "https://youtube.com",
        "https://t.me",
        2,
        true,
        0.15m,
        false,
        "المنصة في أعمال الصيانة حالياً، سنعود قريباً.",
        0.01m,
        0.005m,
        false,
        0.70m,
        0.98m,
        5,
        5
    );
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
                GetDecimal(settings, PlatformSettingKeys.HintPenaltyPercentage, CachedPlatformSettings.Default.HintPenaltyPercentage, minValue: 0m),
                GetString(settings, PlatformSettingKeys.PlatformName, CachedPlatformSettings.Default.PlatformName),
                GetString(settings, PlatformSettingKeys.SupportPhoneNumber, CachedPlatformSettings.Default.SupportPhoneNumber),
                GetString(settings, PlatformSettingKeys.SupportWhatsAppUrl, CachedPlatformSettings.Default.SupportWhatsAppUrl),
                GetString(settings, PlatformSettingKeys.YouTubeChannelUrl, CachedPlatformSettings.Default.YouTubeChannelUrl),
                GetString(settings, PlatformSettingKeys.TelegramChannelUrl, CachedPlatformSettings.Default.TelegramChannelUrl),
                GetInt(settings, PlatformSettingKeys.MaxActiveDevicesPerStudent, CachedPlatformSettings.Default.MaxActiveDevicesPerStudent, minValue: 1),
                GetBool(settings, PlatformSettingKeys.EnableWatermark, CachedPlatformSettings.Default.EnableWatermark),
                GetDecimal(settings, PlatformSettingKeys.WatermarkOpacity, CachedPlatformSettings.Default.WatermarkOpacity, minValue: 0m),
                GetBool(settings, PlatformSettingKeys.MaintenanceMode, CachedPlatformSettings.Default.MaintenanceMode),
                GetString(settings, PlatformSettingKeys.MaintenanceMessage, CachedPlatformSettings.Default.MaintenanceMessage),
                GetDecimal(settings, PlatformSettingKeys.BunnyStreamStorageRateUsdPerGb, CachedPlatformSettings.Default.BunnyStreamStorageRateUsdPerGb, minValue: 0m),
                GetDecimal(settings, PlatformSettingKeys.BunnyStreamBandwidthRateUsdPerGb, CachedPlatformSettings.Default.BunnyStreamBandwidthRateUsdPerGb, minValue: 0m),
                GetBool(settings, PlatformSettingKeys.LiveSupportEnabled, CachedPlatformSettings.Default.LiveSupportEnabled),
                GetDecimal(settings, PlatformSettingKeys.PlayerShadowTopOpacity, CachedPlatformSettings.Default.PlayerShadowTopOpacity, minValue: 0m, maxValue: 1m),
                GetDecimal(settings, PlatformSettingKeys.PlayerShadowBottomOpacity, CachedPlatformSettings.Default.PlayerShadowBottomOpacity, minValue: 0m, maxValue: 1m),
                GetInt(settings, PlatformSettingKeys.YouTubePlayerShadowHideDelaySeconds, CachedPlatformSettings.Default.YouTubePlayerShadowHideDelaySeconds, minValue: 0, maxValue: 60),
                GetInt(settings, PlatformSettingKeys.BunnyPlayerShadowHideDelaySeconds, CachedPlatformSettings.Default.BunnyPlayerShadowHideDelaySeconds, minValue: 0, maxValue: 60)
            );
        })!;
    }

    public void Invalidate()
    {
        _memoryCache.Remove(CacheKey);
    }

    private static int GetInt(IReadOnlyDictionary<string, string> settings, string key, int fallback, int minValue, int maxValue = int.MaxValue)
    {
        return settings.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed) && parsed >= minValue && parsed <= maxValue
            ? parsed
            : fallback;
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, string> settings, string key, decimal fallback, decimal minValue, decimal maxValue = decimal.MaxValue)
    {
        return settings.TryGetValue(key, out var rawValue) && decimal.TryParse(rawValue, out var parsed) && parsed >= minValue && parsed <= maxValue
            ? parsed
            : fallback;
    }

    private static string GetString(IReadOnlyDictionary<string, string> settings, string key, string fallback)
    {
        return settings.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue)
            ? rawValue
            : fallback;
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> settings, string key, bool fallback)
    {
        return settings.TryGetValue(key, out var rawValue) && bool.TryParse(rawValue, out var parsed)
            ? parsed
            : fallback;
    }
}
