using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<ApiResponse<LoginResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, ApiResponse<LoginResponse>>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _config;

    public RefreshTokenCommandHandler(IAppDbContext db, ITokenService tokens, IConfiguration config)
    {
        _db = db;
        _tokens = tokens;
        _config = config;
    }

    public async Task<ApiResponse<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var storedToken = await _db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(r => r.User).ThenInclude(u => u.StudentProfile)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken && !r.IsRevoked, ct)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            storedToken.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Refresh token has expired");
        }

        // Revoke old token (rotation)
        storedToken.IsRevoked = true;

        var user = storedToken.User;
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var isStaff = roles.Any(r => !string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase));

        var newAccessToken = isStaff 
            ? _tokens.GenerateAccessToken(user, roles)
            : _tokens.GenerateAccessToken(user, roles, TimeSpan.FromDays(365));
        var newRefreshToken = _tokens.GenerateRefreshToken();

        var refreshDays = isStaff 
            ? int.Parse(_config["JwtSettings:RefreshExpirationDays"] ?? "30")
            : 365;
        _db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            DeviceFingerprint = storedToken.DeviceFingerprint
        });

        await _db.SaveChangesAsync(ct);

        var userDto = new UserDto(user.Id, user.FullName, user.PhoneNumber, roles, user.IsProfileComplete, user.StudentProfile?.AvatarSlug);
        return ApiResponse<LoginResponse>.Ok(new LoginResponse(newAccessToken, newRefreshToken, userDto));
    }
}
