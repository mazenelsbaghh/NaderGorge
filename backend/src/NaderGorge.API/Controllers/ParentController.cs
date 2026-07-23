using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Reports.Queries;
using NaderGorge.Application.Features.Parent.Commands;
using NaderGorge.Application.Features.Parent.Queries;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/parent")]
public class ParentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public ParentController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    [HttpPost("verify-code")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyParentCodeRequest request, CancellationToken ct)
    {
        var command = new VerifyParentCodeCommand(request.TrackingCode, request.DeviceToken, request.Platform);
        var result = await _mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("student-details")]
    [Authorize(Policy = "RequireParent")]
    public async Task<IActionResult> GetStudentDetails(CancellationToken ct)
    {
        var studentIdClaim = User.FindFirst("StudentId")?.Value;
        if (!Guid.TryParse(studentIdClaim, out var studentProfileId))
        {
            return Unauthorized(ApiResponse.Fail("غير مصرح بالوصول لبيانات الطالب"));
        }

        var query = new GetStudentAcademicDetailsQuery(studentProfileId);
        var result = await _mediator.Send(query, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("device-token")]
    [Authorize(Policy = "RequireParent")]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterParentDeviceTokenRequest request, CancellationToken ct)
    {
        var studentIdClaim = User.FindFirst("StudentId")?.Value;
        if (!Guid.TryParse(studentIdClaim, out var studentProfileId))
        {
            return Unauthorized(ApiResponse.Fail("غير مصرح بتحديث إشعارات الطالب"));
        }

        var command = new RegisterParentDeviceTokenCommand(
            studentProfileId,
            request.DeviceToken,
            request.Platform ?? "android"
        );
        var result = await _mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("notifications")]
    [Authorize(Policy = "RequireParent")]
    public async Task<IActionResult> GetNotifications(CancellationToken ct)
    {
        if (!TryGetParentStudentId(out var studentProfileId))
        {
            return Unauthorized(ApiResponse.Fail("غير مصرح بقراءة تنبيهات الطالب"));
        }

        var result = await _mediator.Send(new GetStudentNotificationsQuery(studentProfileId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("notifications/{id:guid}/read")]
    [Authorize(Policy = "RequireParent")]
    public async Task<IActionResult> MarkNotificationAsRead(Guid id, CancellationToken ct)
    {
        if (!TryGetParentStudentId(out var studentProfileId))
        {
            return Unauthorized(ApiResponse.Fail("غير مصرح بتحديث تنبيهات الطالب"));
        }

        var result = await _mediator.Send(new MarkNotificationAsReadCommand(id, studentProfileId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("app-config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAppConfig(
        [FromServices] ICachedPlatformSettingsReader settingsReader,
        CancellationToken ct)
    {
        var settings = await settingsReader.GetAsync(ct);
        return Ok(ApiResponse<ParentAppConfigResponse>.Ok(new ParentAppConfigResponse(
            settings.ParentAppUpdateRequired,
            settings.ParentAppUpdateUrl,
            settings.ParentAppUpdateMessage
        )));
    }

    [HttpGet("reports/{studentId}/summary")]
    [AllowAnonymous]
    [EnableRateLimiting("parent-report")]
    public async Task<IActionResult> GetSummaryReport(Guid studentId, [FromQuery] string? token, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (!TryValidateParentReportToken(studentId, token, out var error))
            return Unauthorized(ApiResponse.Fail(error));

        var query = new GetParentReportQuery(studentId);
        var result = await _mediator.Send(query, ct);

        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("reports/{studentId}/links")]
    [Authorize(Roles = "Admin")]
    public IActionResult CreateParentReportLink(Guid studentId)
    {
        var expirationHours = GetParentReportLinkExpirationHours();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(expirationHours);
        var token = CreateParentReportToken(studentId, expiresAt);
        return Ok(ApiResponse<object>.Ok(new
        {
            token,
            expiresAt,
            expiresInHours = expirationHours,
            expiresInDays = Math.Max(1, (int)Math.Ceiling(expirationHours / 24.0))
        }));
    }

    private int GetParentReportLinkExpirationHours()
    {
        var configured = _configuration.GetValue<int?>("ParentReports:PublicLinkExpirationHours");
        return configured is > 0 and <= 168 ? configured.Value : 24;
    }

    private bool TryGetParentStudentId(out Guid studentProfileId)
    {
        var studentIdClaim = User.FindFirst("StudentId")?.Value;
        return Guid.TryParse(studentIdClaim, out studentProfileId);
    }

    private string CreateParentReportToken(Guid studentId, DateTimeOffset expiresAt)
    {
        var payload = new ParentReportTokenPayload(
            studentId,
            "parent-report",
            expiresAt.ToUnixTimeSeconds()
        );
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signaturePart = Base64UrlEncode(Sign(payloadPart));
        return $"{payloadPart}.{signaturePart}";
    }

    private bool TryValidateParentReportToken(Guid studentId, string? token, out string error)
    {
        error = "رابط التقرير غير صالح أو منتهي الصلاحية.";
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.', 2);
        if (parts.Length != 2) return false;

        var expectedSignature = Base64UrlEncode(Sign(parts[0]));
        if (!FixedTimeEquals(parts[1], expectedSignature)) return false;

        ParentReportTokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ParentReportTokenPayload>(
                Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
        }
        catch
        {
            return false;
        }

        if (payload is null) return false;
        if (payload.Purpose != "parent-report") return false;
        if (payload.StudentId != studentId) return false;
        if (payload.Exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;

        error = string.Empty;
        return true;
    }

    private byte[] Sign(string payloadPart)
    {
        var secret = _configuration["ParentReports:SigningSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Parent report signing secret is not configured.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private sealed record ParentReportTokenPayload(Guid StudentId, string Purpose, long Exp);
}

public class VerifyParentCodeRequest
{
    public string TrackingCode { get; set; } = string.Empty;
    public string? DeviceToken { get; set; }
    public string? Platform { get; set; }
}

public class RegisterParentDeviceTokenRequest
{
    public string DeviceToken { get; set; } = string.Empty;
    public string? Platform { get; set; }
}

public sealed record ParentAppConfigResponse(
    bool UpdateRequired,
    string UpdateUrl,
    string UpdateMessage
);
