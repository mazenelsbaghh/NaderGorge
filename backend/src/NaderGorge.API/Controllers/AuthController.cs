using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Auth.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public AuthController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

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

        var appSurface = HttpContext.Request.Headers["X-App-Surface"].ToString();
        if (string.IsNullOrWhiteSpace(appSurface))
        {
            var referer = HttpContext.Request.Headers["Referer"].ToString() ?? string.Empty;
            var host = HttpContext.Request.Headers["Host"].ToString() ?? string.Empty;
            if (referer.Contains("admin.", StringComparison.OrdinalIgnoreCase) ||
                host.Contains("admin.", StringComparison.OrdinalIgnoreCase) ||
                referer.Contains("localhost:8740", StringComparison.OrdinalIgnoreCase) ||
                host.Contains("localhost:8740", StringComparison.OrdinalIgnoreCase))
            {
                appSurface = "admin";
            }
            else if (referer.Contains("teacher.", StringComparison.OrdinalIgnoreCase) ||
                     host.Contains("teacher.", StringComparison.OrdinalIgnoreCase) ||
                     referer.Contains("localhost:8741", StringComparison.OrdinalIgnoreCase) ||
                     host.Contains("localhost:8741", StringComparison.OrdinalIgnoreCase))
            {
                appSurface = "teacher";
            }
            else if (referer.Contains("staff.", StringComparison.OrdinalIgnoreCase) ||
                     host.Contains("staff.", StringComparison.OrdinalIgnoreCase) ||
                     referer.Contains("localhost:8742", StringComparison.OrdinalIgnoreCase) ||
                     host.Contains("localhost:8742", StringComparison.OrdinalIgnoreCase))
            {
                appSurface = "assistant";
            }
            else
            {
                appSurface = "student";
            }
        }

        var command = new LoginCommand(
            request.PhoneNumber,
            request.Password,
            request.DeviceFingerprint,
            request.DeviceName,
            ipAddress,
            appSurface);

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
            // Note: Clear the cookie with the same domain configuration it was set with
            ClearRefreshCookie();
            return Unauthorized(result);
        }

        SetRefreshCookie(result.Data.RefreshToken);
        return Ok(ToClientResponse(result));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto? request)
    {
        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = Request.Cookies[RefreshCookieName];
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _mediator.Send(new LogoutCommand(refreshToken));
        }

        ClearRefreshCookie();
        return Ok(ApiResponse.Ok("Logged out successfully"));
    }

    [Authorize]
    [HttpPost("complete-profile")]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileRequest body)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new CompleteProfileCommand(userId, body.ParentPhone, body.Governorate, body.District, body.SchoolName);
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
        var cookieDomain = _configuration["CookieSettings:Domain"];
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/refresh",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };

        if (!string.IsNullOrWhiteSpace(cookieDomain))
        {
            cookieOptions.Domain = cookieDomain;
        }

        Response.Cookies.Append(RefreshCookieName, refreshToken, cookieOptions);
    }

    private void ClearRefreshCookie()
    {
        var cookieDomain = _configuration["CookieSettings:Domain"];
        var cookieOptions = new CookieOptions
        {
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/refresh"
        };

        if (!string.IsNullOrWhiteSpace(cookieDomain))
        {
            cookieOptions.Domain = cookieDomain;
        }

        Response.Cookies.Delete(RefreshCookieName, cookieOptions);
    }
}

public record CompleteProfileRequest(string ParentPhone, string Governorate, string District, string SchoolName);
public record CurrentUserResponse(Guid Id, string FullName, string Phone, string[] Roles, bool ProfileComplete);
public record LoginRequest(string PhoneNumber, string Password, string DeviceFingerprint, string? DeviceName);
public record RefreshRequest(string? RefreshToken);
public record LogoutRequestDto(string? RefreshToken);
public record AuthClientResponse(string AccessToken, UserDto User);
