using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

// ---- Login Command ----
public record LoginCommand(string PhoneNumber, string Password, string DeviceFingerprint, string? DeviceName) : IRequest<ApiResponse<LoginResponse>>;
public record LoginResponse(string AccessToken, string RefreshToken, UserDto User);
public record UserDto(Guid Id, string FullName, string Phone, string[] Roles, bool ProfileComplete, string? AvatarSlug);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.DeviceFingerprint).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, ApiResponse<LoginResponse>>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _config;

    public LoginCommandHandler(IAppDbContext db, ITokenService tokens, IConfiguration config)
    {
        _db = db;
        _tokens = tokens;
        _config = config;
    }

    public async Task<ApiResponse<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Devices)
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber, ct)
            ?? throw new UnauthorizedAccessException("Invalid phone number or password");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is disabled. Contact your administrator.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid phone number or password");

        // --- Device limit enforcement ---
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var isStaff = roles.Any(r => r is "Admin" or "Assistant" or "Teacher");

        var maxDevices = int.Parse(_config["DeviceLimits:MaxDevicesPerStudent"] ?? "2");
        var existingDevice = user.Devices.FirstOrDefault(d => d.DeviceFingerprint == request.DeviceFingerprint && d.IsActive);

        if (existingDevice == null)
        {
            if (!isStaff)
            {
                var activeDeviceCount = user.Devices.Count(d => d.IsActive);
                if (activeDeviceCount >= maxDevices)
                    throw new InvalidOperationException($"Maximum device limit ({maxDevices}) reached. Contact admin to remove a device.");
            }

            var newDevice = new Device
            {
                UserId = user.Id,
                DeviceFingerprint = request.DeviceFingerprint,
                DeviceName = request.DeviceName,
                LastUsedAt = DateTime.UtcNow
            };
            _db.Devices.Add(newDevice);
        }
        else
        {
            existingDevice.LastUsedAt = DateTime.UtcNow;
        }

        // --- Generate tokens ---
        var accessToken = _tokens.GenerateAccessToken(user, roles);
        var refreshToken = _tokens.GenerateRefreshToken();

        var refreshDays = int.Parse(_config["JwtSettings:RefreshExpirationDays"] ?? "30");
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            DeviceFingerprint = request.DeviceFingerprint
        });

        await _db.SaveChangesAsync(ct);

        var userDto = new UserDto(user.Id, user.FullName, user.PhoneNumber, roles, user.IsProfileComplete, user.StudentProfile?.AvatarSlug);
        return ApiResponse<LoginResponse>.Ok(new LoginResponse(accessToken, refreshToken, userDto));
    }
}
