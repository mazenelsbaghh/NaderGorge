using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Parent.Commands;

public record RegisterParentDeviceTokenCommand(
    Guid StudentProfileId,
    string DeviceToken,
    string Platform
) : IRequest<ApiResponse<bool>>;

public class RegisterParentDeviceTokenCommandHandler : IRequestHandler<RegisterParentDeviceTokenCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public RegisterParentDeviceTokenCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(RegisterParentDeviceTokenCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceToken)
            || request.DeviceToken.Trim().EndsWith("-parent-pending-token", StringComparison.OrdinalIgnoreCase))
        {
            // Linking may call this endpoint before the native push SDK has
            // produced a token. Treat the sentinel as a no-op and wait for the
            // subsequent APNs/FCM callback.
            return ApiResponse<bool>.Ok(true);
        }

        var normalizedToken = request.DeviceToken.Trim();
        var platform = string.IsNullOrWhiteSpace(request.Platform) ? "android" : request.Platform.Trim().ToLowerInvariant();

        var existing = await _db.ParentDeviceTokens
            .FirstOrDefaultAsync(t => t.StudentId == request.StudentProfileId && t.DeviceToken == normalizedToken, ct);

        if (existing is null)
        {
            _db.ParentDeviceTokens.Add(new ParentDeviceToken
            {
                StudentId = request.StudentProfileId,
                DeviceToken = normalizedToken,
                Platform = platform
            });
            await _db.SaveChangesAsync(ct);
        }
        else if (!string.Equals(existing.Platform, platform, StringComparison.OrdinalIgnoreCase))
        {
            existing.Platform = platform;
            await _db.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.Ok(true);
    }
}
