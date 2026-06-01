using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdatePlatformSettingsCommand(Dictionary<string, string> Settings) : IRequest<ApiResponse<bool>>;

public class UpdatePlatformSettingsCommandHandler : IRequestHandler<UpdatePlatformSettingsCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly ICachedPlatformSettingsReader _cachedPlatformSettingsReader;

    public UpdatePlatformSettingsCommandHandler(IAppDbContext db, ICachedPlatformSettingsReader cachedPlatformSettingsReader)
    {
        _db = db;
        _cachedPlatformSettingsReader = cachedPlatformSettingsReader;
    }

    public async Task<ApiResponse<bool>> Handle(UpdatePlatformSettingsCommand request, CancellationToken cancellationToken)
    {
        foreach (var kvp in request.Settings)
        {
            var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == kvp.Key, cancellationToken);
            if (setting != null)
            {
                setting.Value = kvp.Value;
            }
            else
            {
                _db.PlatformSettings.Add(new PlatformSetting { Key = kvp.Key, Value = kvp.Value });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        _cachedPlatformSettingsReader.Invalidate();
        return ApiResponse<bool>.Ok(true);
    }
}
