using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.Public.Queries;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ISharedFileStorage _sharedStorage;

    public PublicController(
        IMediator mediator,
        IAppDbContext db,
        IConfiguration config,
        IWebHostEnvironment env,
        ISharedFileStorage sharedStorage)
    {
        _mediator = mediator;
        _db = db;
        _config = config;
        _env = env;
        _sharedStorage = sharedStorage;
    }

    [HttpGet("stats")]
    [AllowAnonymous]
    [OutputCache(Duration = 300)]
    public async Task<IActionResult> GetPlatformStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlatformStatsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("settings")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetPublicSettings(
        [FromServices] NaderGorge.Application.Common.ICachedPlatformSettingsReader settingsReader,
        CancellationToken ct)
    {
        var settings = await settingsReader.GetAsync(ct);
        return Ok(new
        {
            PlatformName = settings.PlatformName,
            SupportPhoneNumber = settings.SupportPhoneNumber,
            SupportWhatsAppUrl = settings.SupportWhatsAppUrl,
            LiveSupportEnabled = settings.LiveSupportEnabled,
            ShowSupportOutsideAccount = settings.ShowSupportOutsideAccount,
            GuestSupportWhatsAppNumber = settings.GuestSupportWhatsAppNumber,
            YouTubeChannelUrl = settings.YouTubeChannelUrl,
            TelegramChannelUrl = settings.TelegramChannelUrl,
            MaintenanceMode = settings.MaintenanceMode,
            MaintenanceMessage = settings.MaintenanceMessage,
            EnableWatermark = settings.EnableWatermark,
            WatermarkOpacity = settings.WatermarkOpacity,
            PlayerShadowTopOpacity = settings.PlayerShadowTopOpacity,
            PlayerShadowBottomOpacity = settings.PlayerShadowBottomOpacity,
            YouTubePlayerShadowHideDelaySeconds = settings.YouTubePlayerShadowHideDelaySeconds,
            BunnyPlayerShadowHideDelaySeconds = settings.BunnyPlayerShadowHideDelaySeconds,
            PlayerShadowTopCoverage = settings.PlayerShadowTopCoverage,
            PlayerShadowBottomCoverage = settings.PlayerShadowBottomCoverage,
            EnabledPlayerShadowProviders = settings.EnabledPlayerShadowProviders,
            PlayerShadowTopSolid = settings.PlayerShadowTopSolid,
            PlayerShadowBottomSolid = settings.PlayerShadowBottomSolid
        });
    }

    [HttpGet("popup")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetPlatformPopup(CancellationToken ct)
    {
        var popupSettingKeys = new[]
        {
            PlatformSettingKeys.PlatformPopupEnabled,
            PlatformSettingKeys.PlatformPopupTitle,
            PlatformSettingKeys.PlatformPopupBody,
            PlatformSettingKeys.PlatformPopupImageUrl,
            PlatformSettingKeys.PlatformPopupActionUrl,
            PlatformSettingKeys.PlatformPopupActionLabel,
            PlatformSettingKeys.PlatformPopupDisplayInterval,
            PlatformSettingKeys.PlatformPopupExpiresAt,
        };

        var popupSettingRows = await _db.PlatformSettings
            .Where(setting => popupSettingKeys.Contains(setting.Key))
            .AsNoTracking()
            .ToListAsync(ct);

        var popupSettingsByKey = popupSettingRows.ToDictionary(setting => setting.Key, setting => setting.Value, StringComparer.Ordinal);
        var revision = popupSettingRows.Count == 0
            ? "0"
            : popupSettingRows.Max(setting => (setting.UpdatedAt ?? setting.CreatedAt).Ticks).ToString();
        var expiresAt = popupSettingsByKey.TryGetValue(PlatformSettingKeys.PlatformPopupExpiresAt, out var expiresAtValue)
            && DateTimeOffset.TryParse(expiresAtValue, out var parsedExpiresAt)
            ? parsedExpiresAt
            : (DateTimeOffset?)null;
        var isEnabled = popupSettingsByKey.TryGetValue(PlatformSettingKeys.PlatformPopupEnabled, out var enabledValue)
            && bool.TryParse(enabledValue, out var parsedEnabled)
            && parsedEnabled
            && (!expiresAt.HasValue || expiresAt.Value > DateTimeOffset.UtcNow);

        return Ok(new
        {
            enabled = isEnabled,
            title = popupSettingsByKey.GetValueOrDefault(PlatformSettingKeys.PlatformPopupTitle, string.Empty),
            body = popupSettingsByKey.GetValueOrDefault(PlatformSettingKeys.PlatformPopupBody, string.Empty),
            imageUrl = popupSettingsByKey.GetValueOrDefault(PlatformSettingKeys.PlatformPopupImageUrl, string.Empty),
            actionUrl = popupSettingsByKey.GetValueOrDefault(PlatformSettingKeys.PlatformPopupActionUrl, string.Empty),
            actionLabel = popupSettingsByKey.GetValueOrDefault(PlatformSettingKeys.PlatformPopupActionLabel, string.Empty),
            displayInterval = popupSettingsByKey.GetValueOrDefault(PlatformSettingKeys.PlatformPopupDisplayInterval, "0"),
            expiresAt,
            revision,
        });
    }

    [HttpGet("packages/{packageId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicPackage(Guid packageId, CancellationToken ct)
    {
        var package = await _db.Packages
            .AsNoTracking()
            .Where(item => item.Id == packageId
                && item.IsActive
                && item.Teacher.IsVisibleToStudents
                && item.Teacher.IsContentVisibleToStudents
                && item.Teacher.User.IsActive
                && !item.Teacher.User.IsDeleted)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Description,
                item.Price,
                item.ImageUrl,
                SubjectName = item.Subject.Name,
                TeacherName = item.Teacher.User.FullName,
                TeacherId = item.TeacherId,
                Terms = item.Terms
                    .OrderBy(term => term.Order)
                    .Select(term => new
                    {
                        term.Id,
                        term.Title,
                        term.Price,
                        term.ImageUrl,
                        Sections = term.Sections
                            .OrderBy(section => section.Order)
                            .Select(section => new
                            {
                                section.Id,
                                section.Title,
                                Lessons = section.Lessons
                                    .OrderBy(lesson => lesson.Order)
                                    .Select(lesson => new { lesson.Id, lesson.Title })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        return package is null
            ? NotFound(new { success = false, message = "الباقة غير متاحة" })
            : Ok(new { success = true, data = package });
    }

    [HttpGet("active-teachers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTeachers(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetActiveTeachersQuery(), ct);
        return Ok(result);
    }

    [HttpGet("resources/{resourceId:guid}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadResource(Guid resourceId, [FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest(new { Success = false, Message = "Token is required." });

        var secret = _config["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(secret))
            return StatusCode(500, new { Success = false, Message = "Server configuration error" });

        try
        {
            var decodedBytes = Convert.FromBase64String(token);
            var decoded = Encoding.UTF8.GetString(decodedBytes);
            var parts = decoded.Split(':');
            if (parts.Length != 3)
                return BadRequest(new { Success = false, Message = "Invalid token format." });

            var userIdStr = parts[0];
            var expiresStr = parts[1];
            var signature = parts[2];

            if (!long.TryParse(expiresStr, out var expiresUnixSeconds))
                return BadRequest(new { Success = false, Message = "Invalid token expiry." });

            if (expiresUnixSeconds < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                return BadRequest(new { Success = false, Message = "Token has expired." });

            var payload = $"{userIdStr}:{expiresStr}";
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{resourceId}:{payload}"));
            var expectedSignature = Convert.ToHexString(hashBytes);

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(signature)))
            {
                return BadRequest(new { Success = false, Message = "Invalid token signature." });
            }
        }
        catch (FormatException)
        {
            return BadRequest(new { Success = false, Message = "Invalid token format." });
        }

        var resource = await _db.LessonResources.FirstOrDefaultAsync(r => r.Id == resourceId, ct);
        if (resource == null)
            return NotFound(new { Success = false, Message = "Resource not found." });

        var fileUrl = resource.FileUrl;
        string relativePath;
        if (fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(fileUrl);
            relativePath = uri.AbsolutePath.TrimStart('/');
        }
        else
        {
            relativePath = fileUrl.TrimStart('/');
        }

        if (Path.IsPathRooted(relativePath) || relativePath.Contains(".."))
        {
            return BadRequest(new { Success = false, Message = "Invalid resource path." });
        }

        var protectedPrefix = "protected/resources/";
        var usesProtectedStorage = relativePath.StartsWith(protectedPrefix, StringComparison.OrdinalIgnoreCase);
        var storageRelativePath = usesProtectedStorage
            ? relativePath[protectedPrefix.Length..]
            : relativePath;
        var area = usesProtectedStorage
            ? SharedFileArea.Protected
            : SharedFileArea.Public;
        var areaRelativePath = usesProtectedStorage
            ? Path.Combine("resources", storageRelativePath)
            : storageRelativePath;

        var isDocker = _env.EnvironmentName == "Docker" || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Docker";
        try
        {
            var stream = await _sharedStorage.OpenReadAsync(area, areaRelativePath, ct);
            if (isDocker)
            {
                await stream.DisposeAsync();
                var accelPath = usesProtectedStorage
                    ? $"protected/resources/{storageRelativePath.Replace(Path.DirectorySeparatorChar, '/')}"
                    : relativePath;
                Response.Headers.Append("X-Accel-Redirect", $"/secured-assets/{accelPath}");
                Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{Uri.EscapeDataString(Path.GetFileName(storageRelativePath))}\"");
                return new EmptyResult();
            }
            return File(
                stream,
                "application/octet-stream",
                Path.GetFileName(storageRelativePath),
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { Success = false, Message = "File not found on disk." });
        }
    }
}
