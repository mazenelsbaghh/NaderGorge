using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

// ---- Login Command ----
public record LoginCommand(string PhoneNumber, string Password, string DeviceFingerprint, string? DeviceName, string? IpAddress, string? AppSurface = null) : IRequest<ApiResponse<LoginResponse>>;
public record LoginResponse(string AccessToken, string RefreshToken, UserDto User);
public record UserDto(Guid Id, string FullName, string Phone, string[] Roles, string[] Permissions, bool ProfileComplete, string? AvatarSlug);

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
    private readonly ICachedPlatformSettingsReader _settingsReader;

    public LoginCommandHandler(IAppDbContext db, ITokenService tokens, IConfiguration config, ICachedPlatformSettingsReader settingsReader)
    {
        _db = db;
        _tokens = tokens;
        _config = config;
        _settingsReader = settingsReader;
    }

    public async Task<ApiResponse<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Devices)
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber, ct)
            ?? throw new UnauthorizedAccessException("Invalid phone number or password");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var isStaff = roles.Any(r => !string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase));

        var isStaffSurface = 
            string.Equals(request.AppSurface, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.AppSurface, "assistant", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.AppSurface, "staff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.AppSurface, "teacher", StringComparison.OrdinalIgnoreCase);

        if (isStaffSurface)
        {
            if (!isStaff)
            {
                throw new UnauthorizedAccessException("Invalid phone number or password");
            }
        }
        else // student or landing
        {
            var isStudent = roles.Contains("Student");
            if (!isStudent)
            {
                throw new UnauthorizedAccessException("Invalid phone number or password");
            }
        }

        if (!user.IsActive)
        {
            var platformSettings = await _settingsReader.GetAsync(ct);
            var reason = user.SuspensionReason;
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "مخالفة شروط الاستخدام";
            }
            var reasonText = !string.IsNullOrWhiteSpace(reason) ? $" السبب: {reason}." : "";
            throw new UnauthorizedAccessException($"تم تعطيل الحساب.{reasonText} برجاء التواصل مع الدعم الفني: {platformSettings.SupportPhoneNumber}");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid phone number or password");

        // --- Device limit enforcement ---

        var dynamicSettings = await _settingsReader.GetAsync(ct);
        var maxDevices = dynamicSettings.MaxActiveDevicesPerStudent;
        var existingDevice = user.Devices.FirstOrDefault(d => d.DeviceFingerprint == request.DeviceFingerprint && d.IsActive);

        if (existingDevice == null)
        {
            if (!isStaff)
            {
                var activeDeviceCount = user.Devices.Count(d => d.IsActive);
                if (activeDeviceCount >= maxDevices)
                    throw new InvalidOperationException($"Maximum device limit ({maxDevices}) reached. Contact admin to remove a device.");
            }

            var (osName, browserName, deviceType) = UserAgentParser.Parse(request.DeviceName);
            var newDevice = new Device
            {
                UserId = user.Id,
                DeviceFingerprint = request.DeviceFingerprint,
                DeviceName = request.DeviceName,
                IpAddress = request.IpAddress,
                OsName = osName,
                BrowserName = browserName,
                DeviceType = deviceType,
                LastUsedAt = DateTime.UtcNow
            };
            _db.Devices.Add(newDevice);
        }
        else
        {
            existingDevice.LastUsedAt = DateTime.UtcNow;
            existingDevice.IpAddress = request.IpAddress ?? existingDevice.IpAddress;
        }

        // --- Generate tokens ---
        var accessToken = isStaff
            ? _tokens.GenerateAccessToken(user, roles)
            : _tokens.GenerateAccessToken(user, roles, TimeSpan.FromDays(365));
        var refreshToken = _tokens.GenerateRefreshToken();

        var refreshDays = isStaff
            ? int.Parse(_config["JwtSettings:RefreshExpirationDays"] ?? "30")
            : 365;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            DeviceFingerprint = request.DeviceFingerprint
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
        return ApiResponse<LoginResponse>.Ok(new LoginResponse(accessToken, refreshToken, userDto));
    }
}
