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
        if (string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            return ApiResponse<bool>.Fail("رمز الجهاز غير صالح");
        }

        var normalizedToken = request.DeviceToken.Trim();
        var platform = string.IsNullOrWhiteSpace(request.Platform) ? "android" : request.Platform.Trim();

        var exists = await _db.ParentDeviceTokens
            .AnyAsync(t => t.StudentId == request.StudentProfileId && t.DeviceToken == normalizedToken, ct);

        if (!exists)
        {
            _db.ParentDeviceTokens.Add(new ParentDeviceToken
            {
                StudentId = request.StudentProfileId,
                DeviceToken = normalizedToken,
                Platform = platform
            });
            await _db.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.Ok(true);
    }
}
