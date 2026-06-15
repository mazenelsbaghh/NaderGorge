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
        // Atomically revoke the token. If it's already revoked or doesn't exist, rowsAffected will be 0.
        var rowsAffected = await _db.RefreshTokens
            .Where(r => r.Token == request.RefreshToken && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), ct);

        if (rowsAffected == 0)
        {
            throw new UnauthorizedAccessException("Refresh token has been replayed or is invalid");
        }

        // Now load the token to verify expiration and get user details
        var storedToken = await _db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(r => r.User).ThenInclude(u => u.StudentProfile)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token has expired");
        }

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

        var permissionsList = new List<string>();
        foreach (var ur in user.UserRoles)
        {
            if (ur.Role != null && !string.IsNullOrEmpty(ur.Role.PermissionsJson))
            {
                try
                {
                    var perms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ur.Role.PermissionsJson);
                    if (perms != null)
                    {
                        permissionsList.AddRange(perms);
                    }
                }
                catch { /* ignore invalid JSON */ }
            }
        }
        var permissions = permissionsList.Distinct().ToArray();

        var userDto = new UserDto(user.Id, user.FullName, user.PhoneNumber, roles, permissions, user.IsProfileComplete, user.StudentProfile?.AvatarSlug);
        return ApiResponse<LoginResponse>.Ok(new LoginResponse(newAccessToken, newRefreshToken, userDto));
    }
}
