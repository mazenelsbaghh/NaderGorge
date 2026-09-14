using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Services;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private readonly WhatsAppVerificationService _whatsAppService;
    private readonly WhatsAppCloudService _whatsAppCloudService;
    private readonly WhatsAppExamNotificationService _whatsAppExamNotificationService;

    public WhatsAppController(
        WhatsAppVerificationService whatsAppService,
        WhatsAppCloudService whatsAppCloudService,
        WhatsAppExamNotificationService whatsAppExamNotificationService)
    {
        _whatsAppService = whatsAppService;
        _whatsAppCloudService = whatsAppCloudService;
        _whatsAppExamNotificationService = whatsAppExamNotificationService;
    }

    public record CheckRequest(string PhoneNumber);

    public record CheckResponse(bool? Exists, string Number);

    public record SendTestMessageRequest(
        string RecipientPhoneNumber,
        string MessageType,
        string? TextBody,
        string? TemplateName,
        string? TemplateLanguage,
        string? ParentName,
        string? StudentName,
        string? Score,
        string? TotalScore,
        string? Subject,
        string? Lecture);

    public record SendExamResultMessageRequest(
        Guid AttemptId,
        string? RecipientPhoneNumber);

    /// <summary>
    /// Check if a phone number is registered on WhatsApp.
    /// Public endpoint — used during registration (no auth required).
    /// </summary>
    [HttpPost("check")]
    [EnableRateLimiting("public-whatsapp")]
    public async Task<IActionResult> CheckWhatsApp([FromBody] CheckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { error = "Phone number is required" });

        // Validate Egyptian phone format: 11 digits starting with 01
        var phone = request.PhoneNumber.Trim();
        if (phone.Length != 11 || !phone.StartsWith("01"))
            return BadRequest(new { error = "Invalid Egyptian phone number format. Must be 11 digits starting with 01." });

        var result = await _whatsAppService.CheckWhatsAppAsync(phone);

        if (result.Exists is null)
        {
            return StatusCode(503, new CheckResponse(null, MaskPhone(result.Number)));
        }

        return Ok(new CheckResponse(result.Exists, MaskPhone(result.Number)));
    }

    [HttpPost("admin/test-message")]
    [Authorize]
    [HasPermission("settings.manage")]
    public async Task<IActionResult> SendAdminTestMessage(
        [FromBody] SendTestMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientPhoneNumber))
        {
            return BadRequest(new { success = false, message = "Recipient phone number is required." });
        }

        var result = await _whatsAppCloudService.SendTestMessageAsync(
            new WhatsAppCloudService.SendTestMessageRequest(
                request.RecipientPhoneNumber,
                request.MessageType,
                request.TextBody,
                request.TemplateName,
                request.TemplateLanguage,
                request.ParentName,
                request.StudentName,
                request.Score,
                request.TotalScore,
                request.Subject,
                request.Lecture),
            cancellationToken);

        return result.Success ? Ok(result) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("admin/exam-result-message")]
    [Authorize]
    [HasPermission("settings.manage")]
    public async Task<IActionResult> SendAdminExamResultMessage(
        [FromBody] SendExamResultMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AttemptId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "Attempt id is required." });
        }

        var result = await _whatsAppExamNotificationService.SendExamResultAsync(
            request.AttemptId,
            request.RecipientPhoneNumber,
            cancellationToken);

        return result.Success ? Ok(result) : StatusCode(result.StatusCode, result);
    }

    private static string MaskPhone(string number)
    {
        if (number.Length < 6) return "***";
        return $"{number[..3]}****{number[^3..]}";
    }
}
