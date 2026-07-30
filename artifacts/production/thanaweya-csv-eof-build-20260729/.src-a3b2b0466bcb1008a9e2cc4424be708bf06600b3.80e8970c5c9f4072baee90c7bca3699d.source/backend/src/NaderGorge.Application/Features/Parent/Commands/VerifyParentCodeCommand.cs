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

        if (studentProfile == null)
        {
            return ApiResponse<VerifyCodeResponse>.Fail("الرمز غير صالح، يرجى التحقق وإعادة المحاولة");
        }

        // Register device token if provided
        if (!string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            var alreadyRegistered = await _db.ParentDeviceTokens
                .AnyAsync(t => t.StudentId == studentProfile.Id && t.DeviceToken == request.DeviceToken, ct);

            if (!alreadyRegistered)
            {
                var parentDeviceToken = new ParentDeviceToken
                {
                    StudentId = studentProfile.Id,
                    DeviceToken = request.DeviceToken,
                    Platform = request.Platform ?? "unknown"
                };
                _db.ParentDeviceTokens.Add(parentDeviceToken);
                await _db.SaveChangesAsync(ct);
            }
        }

        var token = _tokenService.GenerateParentToken(studentProfile.Id);

        return ApiResponse<VerifyCodeResponse>.Ok(new VerifyCodeResponse(token, studentProfile.User.FullName));
    }
}
