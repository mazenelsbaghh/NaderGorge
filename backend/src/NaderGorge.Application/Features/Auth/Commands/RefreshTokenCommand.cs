using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<ApiResponse<LoginResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, ApiResponse<LoginResponse>>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public RefreshTokenCommandHandler(IAppDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<ApiResponse<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        // Atomically revoke the token. If it's already revoked or doesn't exist, rowsAffected will be 0.
        var rowsAffected = await RevokePresentedRefreshTokenAsync(request.RefreshToken, ct);

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
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }

        if (!string.IsNullOrWhiteSpace(storedToken.DeviceFingerprint))
        {
            var deviceIsActive = await _db.Devices.AnyAsync(d =>
                d.UserId == user.Id &&
                d.DeviceFingerprint == storedToken.DeviceFingerprint &&
                d.IsActive,
                ct);

            if (!deviceIsActive)
            {
                throw new UnauthorizedAccessException("Device session is no longer active.");
            }
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var newAccessToken = _tokens.GenerateAccessToken(user, roles, AuthSessionPolicy.Lifetime);
        var newRefreshToken = _tokens.GenerateRefreshToken();

        _db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.Add(AuthSessionPolicy.Lifetime),
            DeviceFingerprint = storedToken.DeviceFingerprint
        });

        await _db.SaveChangesAsync(ct);

        var permissionsList = new List<string>();
        var allowedDomainsList = new List<string>();
        var allowedNavbarItemsList = new List<string>();
        foreach (var ur in user.UserRoles)
        {
            if (ur.Role != null)
            {
                if (!string.IsNullOrEmpty(ur.Role.PermissionsJson))
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

                if (!string.IsNullOrEmpty(ur.Role.AllowedDomain))
                {
                    allowedDomainsList.Add(ur.Role.AllowedDomain);
                }

                if (!string.IsNullOrEmpty(ur.Role.AllowedNavbarItemsJson))
                {
                    try
                    {
                        var items = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ur.Role.AllowedNavbarItemsJson);
                        if (items != null)
                        {
                            allowedNavbarItemsList.AddRange(items);
                        }
                    }
                    catch { /* ignore invalid JSON */ }
                }
            }
        }
        var permissions = permissionsList.Distinct().ToArray();
        var allowedDomains = allowedDomainsList.Distinct().ToArray();
        var allowedNavbarItems = allowedNavbarItemsList.Distinct().ToArray();

        var userDto = new UserDto(user.Id, user.FullName, user.PhoneNumber, roles, permissions, user.IsProfileComplete, user.StudentProfile?.AvatarSlug, allowedDomains, allowedNavbarItems, user.SecurityStampVersion);
        return ApiResponse<LoginResponse>.Ok(new LoginResponse(newAccessToken, newRefreshToken, userDto));
    }

    private async Task<int> RevokePresentedRefreshTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            return await _db.RefreshTokens
                .Where(r => r.Token == token && !r.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ExecuteUpdate", StringComparison.OrdinalIgnoreCase))
        {
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token && !r.IsRevoked, ct);
            if (stored is null) return 0;
            stored.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
            return 1;
        }
    }
}
