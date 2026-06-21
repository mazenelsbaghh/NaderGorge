using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.Public.Queries;
using NaderGorge.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public PublicController(IMediator mediator, IAppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _mediator = mediator;
        _db = db;
        _config = config;
        _env = env;
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
    [OutputCache(Duration = 300)]
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
            EnabledPlayerShadowProviders = settings.EnabledPlayerShadowProviders
        });
    }

    [HttpGet("teachers")]
    [AllowAnonymous]
    [OutputCache(Duration = 300)]
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

        var rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        if (Path.IsPathRooted(relativePath) || relativePath.Contains(".."))
        {
            return BadRequest(new { Success = false, Message = "Invalid resource path." });
        }

        var physicalPath = Path.Combine(rootPath, relativePath);
        var fullPath = Path.GetFullPath(physicalPath);
        var fullRootPath = Path.GetFullPath(rootPath);
        var normalizedRoot = fullRootPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullRootPath
            : fullRootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Success = false, Message = "Invalid resource path." });
        }

        if (!System.IO.File.Exists(physicalPath))
            return NotFound(new { Success = false, Message = "File not found on disk." });

        var isDocker = _env.EnvironmentName == "Docker" || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Docker";
        if (isDocker)
        {
            Response.Headers.Append("X-Accel-Redirect", $"/secured-assets/{relativePath}");
            Response.Headers.Append("Content-Disposition", $"attachment; filename={Uri.EscapeDataString(Path.GetFileName(relativePath))}");
            return new EmptyResult();
        }
        else
        {
            var contentType = "application/octet-stream";
            return PhysicalFile(physicalPath, contentType, Path.GetFileName(relativePath), enableRangeProcessing: true);
        }
    }
}
