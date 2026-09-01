using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Infrastructure.Services;
using Npgsql;

namespace NaderGorge.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/live-support/messenger")]
public sealed class AdminFacebookMessengerController(
    FacebookMessengerAdminService service) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct) =>
        Ok(ApiResponse<FacebookMessengerAdminSettingsDto>.Ok(
            await service.GetSettingsAsync(ct)));

    [HttpPut("settings")]
    [HttpLogging(HttpLoggingFields.None)]
    public Task<IActionResult> UpdateSettings(
        UpdateFacebookMessengerSettingsRequest request,
        CancellationToken ct) =>
        ExecuteAsync(async () => ApiResponse<FacebookMessengerAdminSettingsDto>.Ok(
            await service.UpdateSettingsAsync(
                new FacebookMessengerSettingsUpdate(
                    request.AppId,
                    request.ApiVersion,
                    request.AppSecret,
                    Revision(request.ExpectedRevision)),
                User.RequireUserId(),
                ct)));

    [HttpPost("verify-token/rotate")]
    [HttpLogging(HttpLoggingFields.None)]
    public Task<IActionResult> RotateVerifyToken(
        FacebookMessengerExpectedRevisionRequest request,
        CancellationToken ct) =>
        ExecuteAsync(async () => ApiResponse<FacebookMessengerVerifyTokenRotationDto>.Ok(
            await service.RotateVerifyTokenAsync(
                Revision(request.ExpectedRevision),
                User.RequireUserId(),
                ct)));

    [HttpPost("pages/link")]
    [HttpLogging(HttpLoggingFields.None)]
    public Task<IActionResult> LinkPage(
        LinkFacebookMessengerPageRequest request,
        CancellationToken ct) =>
        ExecuteAsync(async () => ApiResponse<FacebookMessengerAdminPageDto>.Ok(
            await service.LinkPageAsync(
                new FacebookMessengerPageLink(
                    request.AccessToken,
                    request.HumanAgentEnabled,
                    request.ExistingPageRecordId,
                    Revision(request.ExpectedRevision)),
                User.RequireUserId(),
                ct)));

    [HttpPost("pages/{pageRecordId:guid}/check")]
    [HttpLogging(HttpLoggingFields.None)]
    public Task<IActionResult> CheckPage(Guid pageRecordId, CancellationToken ct) =>
        ExecuteAsync(async () => ApiResponse<FacebookMessengerPageCheckDto>.Ok(
            await service.CheckPageAsync(pageRecordId, User.RequireUserId(), ct)));

    [HttpDelete("pages/{pageRecordId:guid}")]
    [HttpLogging(HttpLoggingFields.None)]
    public Task<IActionResult> DeletePage(
        Guid pageRecordId,
        FacebookMessengerExpectedRevisionRequest request,
        CancellationToken ct) =>
        ExecuteAsync(async () => ApiResponse<FacebookMessengerAdminSettingsDto>.Ok(
            await service.DeletePageAsync(
                pageRecordId,
                Revision(request.ExpectedRevision),
                User.RequireUserId(),
                ct)));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<ApiResponse<T>>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (FacebookMessengerAdminException exception)
        {
            return StatusCode(exception.StatusCode, new
            {
                success = false,
                code = exception.ErrorCode,
                message = exception.SafeMessage,
                errors = new[] { exception.ErrorCode }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                success = false,
                code = "MESSENGER_CONFIGURATION_CONFLICT",
                message = "تم تعديل إعدادات Messenger. حدّث الصفحة ثم أعد المحاولة.",
                errors = new[] { "MESSENGER_CONFIGURATION_CONFLICT" }
            });
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgres &&
            postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new
            {
                success = false,
                code = "MESSENGER_CONFIGURATION_CONFLICT",
                message = "تم تعديل إعدادات Messenger بالتزامن. حدّث الصفحة ثم أعد المحاولة.",
                errors = new[] { "MESSENGER_CONFIGURATION_CONFLICT" }
            });
        }
        catch (PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new
            {
                success = false,
                code = "MESSENGER_CONFIGURATION_CONFLICT",
                message = "تم تعديل إعدادات Messenger بالتزامن. حدّث الصفحة ثم أعد المحاولة.",
                errors = new[] { "MESSENGER_CONFIGURATION_CONFLICT" }
            });
        }
    }

    private static long Revision(string value)
    {
        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) &&
            revision >= 0)
            return revision;
        throw new FacebookMessengerAdminException(
            "MESSENGER_CONFIGURATION_CONFLICT",
            "نسخة إعدادات Messenger غير صالحة. حدّث الصفحة وحاول مرة أخرى.",
            StatusCodes.Status409Conflict);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateFacebookMessengerSettingsRequest(
    string AppId,
    string ApiVersion,
    string? AppSecret,
    string ExpectedRevision);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FacebookMessengerExpectedRevisionRequest(string ExpectedRevision);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkFacebookMessengerPageRequest(
    string AccessToken,
    bool HumanAgentEnabled,
    Guid? ExistingPageRecordId,
    string ExpectedRevision);
