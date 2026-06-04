using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Services;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private readonly WhatsAppVerificationService _whatsAppService;

    public WhatsAppController(WhatsAppVerificationService whatsAppService)
    {
        _whatsAppService = whatsAppService;
    }

    public record CheckRequest(string PhoneNumber);

    public record CheckResponse(bool? Exists, string Number);

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

    private static string MaskPhone(string number)
    {
        if (number.Length < 6) return "***";
        return $"{number[..3]}****{number[^3..]}";
    }
}
