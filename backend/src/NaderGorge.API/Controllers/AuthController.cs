using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Auth.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    private const string RefreshCookieName = "ng_refresh";

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
        if (!result.Success || result.Data == null)
        {
            return Unauthorized(result);
        }

        SetRefreshCookie(result.Data.RefreshToken);
        return Ok(ToClientResponse(result));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? request)
    {
        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = Request.Cookies[RefreshCookieName];
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(ApiResponse.Fail("Invalid or expired refresh token"));
        }

        var command = new RefreshTokenCommand(refreshToken);
        var result = await _mediator.Send(command);
        if (!result.Success || result.Data == null)
        {
            ClearRefreshCookie();
            return Unauthorized(result);
        }

        SetRefreshCookie(result.Data.RefreshToken);
        return Ok(ToClientResponse(result));
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

    private ApiResponse<AuthClientResponse> ToClientResponse(ApiResponse<LoginResponse> result)
    {
        return ApiResponse<AuthClientResponse>.Ok(
            new AuthClientResponse(result.Data!.AccessToken, result.Data.User),
            result.Message);
    }

    private void SetRefreshCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/refresh",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/refresh"
        });
    }
}

public record CompleteProfileRequest(string ParentPhone, string Governorate, string City, string School);
public record CurrentUserResponse(Guid Id, string FullName, string Phone, string[] Roles, bool ProfileComplete);
public record LoginRequest(string PhoneNumber, string Password, string DeviceFingerprint, string? DeviceName);
public record RefreshRequest(string? RefreshToken);
public record AuthClientResponse(string AccessToken, UserDto User);
