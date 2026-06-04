using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Reports.Queries;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/parent/reports")]
public class ParentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public ParentController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    [HttpGet("{studentId}/summary")]
    [AllowAnonymous]
    [EnableRateLimiting("parent-report")]
    public async Task<IActionResult> GetSummaryReport(Guid studentId, [FromQuery] string? token, CancellationToken ct)
    {
        if (!TryValidateParentReportToken(studentId, token, out var error))
            return Unauthorized(ApiResponse.Fail(error));

        var query = new GetParentReportQuery(studentId);
        var result = await _mediator.Send(query, ct);

        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{studentId}/links")]
    [Authorize(Roles = "Admin")]
    public IActionResult CreateParentReportLink(Guid studentId)
    {
        var token = CreateParentReportToken(studentId, DateTimeOffset.UtcNow.AddDays(7));
        return Ok(ApiResponse<object>.Ok(new { token, expiresInDays = 7 }));
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
