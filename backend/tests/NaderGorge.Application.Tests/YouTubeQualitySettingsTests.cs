using Microsoft.Extensions.Caching.Memory;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using Xunit;

namespace NaderGorge.Application.Tests;

public class YouTubeQualitySettingsTests
{
    [Theory]
    [InlineData(PlatformSettingKeys.YouTubeQualityBottomCoverPercent, "-1")]
    [InlineData(PlatformSettingKeys.YouTubeQualityBottomCoverPercent, "41")]
    [InlineData(PlatformSettingKeys.YouTubeQualityMobileBottomCoverPercent, "NaN")]
    [InlineData(PlatformSettingKeys.YouTubeQualityMobileBottomCoverPercent, "15.5")]
    public async Task Invalid_cover_height_rejects_the_entire_settings_update(string key, string value)
    {
        await using var db = TestAppDbContextFactory.Create();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new UpdatePlatformSettingsCommandHandler(db, new CachedPlatformSettingsReader(cache, db));
        var result = await handler.Handle(new UpdatePlatformSettingsCommand(new()
        {
            [PlatformSettingKeys.PlatformName] = "Should not be saved",
            [key] = value,
        }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(db.PlatformSettings);
    }
}
