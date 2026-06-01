using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetPlatformSettingsQuery() : IRequest<ApiResponse<List<PlatformSetting>>>;

public class GetPlatformSettingsQueryHandler : IRequestHandler<GetPlatformSettingsQuery, ApiResponse<List<PlatformSetting>>>
{
    private readonly IAppDbContext _db;

    public GetPlatformSettingsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<PlatformSetting>>> Handle(GetPlatformSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.PlatformSettings
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        EnsureDefault(settings, PlatformSettingKeys.VideoWatchThresholdPercentage, CachedPlatformSettings.Default.VideoWatchThresholdPercentage.ToString());
        EnsureDefault(settings, PlatformSettingKeys.MaxExtraWatchRequestsPerVideo, CachedPlatformSettings.Default.MaxExtraWatchRequestsPerVideo.ToString());
        EnsureDefault(settings, PlatformSettingKeys.HintPenaltyPercentage, CachedPlatformSettings.Default.HintPenaltyPercentage.ToString("0.##"));

        settings = settings
            .OrderBy(setting => setting.Key)
            .ToList();

        return ApiResponse<List<PlatformSetting>>.Ok(settings);
    }

    private static void EnsureDefault(List<PlatformSetting> settings, string key, string value)
    {
        if (settings.Any(setting => string.Equals(setting.Key, key, StringComparison.Ordinal)))
        {
            return;
        }

        settings.Add(new PlatformSetting
        {
            Key = key,
            Value = value,
        });
    }
}
