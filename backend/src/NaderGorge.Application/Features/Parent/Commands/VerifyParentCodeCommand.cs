using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Parent.Commands;

public record VerifyParentCodeCommand(
    string TrackingCode,
    string? DeviceToken,
    string? Platform
) : IRequest<ApiResponse<VerifyCodeResponse>>;

public record VerifyCodeResponse(string Token, string StudentName);

public class VerifyParentCodeCommandHandler : IRequestHandler<VerifyParentCodeCommand, ApiResponse<VerifyCodeResponse>>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;

    public VerifyParentCodeCommandHandler(IAppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<ApiResponse<VerifyCodeResponse>> Handle(VerifyParentCodeCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TrackingCode))
        {
            return ApiResponse<VerifyCodeResponse>.Fail("الرمز غير صالح، يرجى التحقق وإعادة المحاولة");
        }

        var trackingCodeNormalized = request.TrackingCode.Trim().ToUpperInvariant();

        var studentProfile = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.ParentTrackingCode == trackingCodeNormalized, ct);

        if (studentProfile?.User is not { IsActive: true, IsDeleted: false })
        {
            return ApiResponse<VerifyCodeResponse>.Fail("الرمز غير صالح، يرجى التحقق وإعادة المحاولة");
        }

        // A client can finish linking before APNs/FCM returns a token. Do not
        // persist the temporary sentinel; the real token is registered later.
        if (!IsPendingDeviceToken(request.DeviceToken))
        {
            var normalizedDeviceToken = request.DeviceToken!.Trim();
            var alreadyRegistered = await _db.ParentDeviceTokens
                .AnyAsync(t => t.StudentId == studentProfile.Id && t.DeviceToken == normalizedDeviceToken, ct);

            if (!alreadyRegistered)
            {
                var parentDeviceToken = new ParentDeviceToken
                {
                    StudentId = studentProfile.Id,
                    DeviceToken = normalizedDeviceToken,
                    Platform = string.IsNullOrWhiteSpace(request.Platform) ? "unknown" : request.Platform.Trim().ToLowerInvariant()
                };
                _db.ParentDeviceTokens.Add(parentDeviceToken);
                await _db.SaveChangesAsync(ct);
            }
        }

        var token = _tokenService.GenerateParentToken(
            studentProfile.User,
            studentProfile.Id);

        return ApiResponse<VerifyCodeResponse>.Ok(new VerifyCodeResponse(token, studentProfile.User.FullName));
    }

    private static bool IsPendingDeviceToken(string? deviceToken)
    {
        return string.IsNullOrWhiteSpace(deviceToken)
            || deviceToken.Trim().EndsWith("-parent-pending-token", StringComparison.OrdinalIgnoreCase);
    }
}
