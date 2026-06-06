using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Features.Auth.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var fullName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var phone = User.FindFirstValue("phone") ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        var profileComplete = string.Equals(
            User.FindFirstValue("profileComplete"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(parsedUserId, fullName, phone, roles, profileComplete));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "Unknown";

        var command = new LoginCommand(
            request.PhoneNumber,
            request.Password,
            request.DeviceFingerprint,
            request.DeviceName,
            ipAddress);

        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpPost("complete-profile")]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileRequest body)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new CompleteProfileCommand(userId, body.ParentPhone, body.Governorate);
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("verify-reset-fields")]
    public async Task<IActionResult> VerifyResetFields([FromBody] VerifyResetFieldsCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record CompleteProfileRequest(string ParentPhone, string Governorate, string City, string School);
public record CurrentUserResponse(Guid Id, string FullName, string Phone, string[] Roles, bool ProfileComplete);
public record LoginRequest(string PhoneNumber, string Password, string DeviceFingerprint, string? DeviceName);
