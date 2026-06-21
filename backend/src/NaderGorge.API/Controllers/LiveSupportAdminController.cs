using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support/admin")]
[HasPermission("live_support.manage")]
public sealed class LiveSupportAdminController(ILiveSupportService service) : ControllerBase
{
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct) => Ok(ApiResponse<LiveSupportAdminConfigDto>.Ok(await service.GetAdminConfigAsync(ct)));

    [HttpPut("feature")]
    public async Task<IActionResult> Feature(UpdateFeatureRequest request, CancellationToken ct)
    {
        await service.SetFeatureEnabledAsync(request.Enabled, ct);
        return Ok(ApiResponse.Ok("تم تحديث حالة الدعم المباشر."));
    }

    [HttpPut("staff/{staffUserId:guid}")]
    public async Task<IActionResult> Staff(Guid staffUserId, UpdateStaffConfigRequest request, CancellationToken ct)
    {
        try
        {
            var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(ApiResponse<LiveSupportStaffConfigDto>.Ok(await service.UpdateStaffConfigAsync(actor, staffUserId, request.Enabled, request.Capacity, request.ExpectedVersion, request.Schedule, ct)));
        }
        catch (LiveSupportException ex)
        {
            return StatusCode(ex.Code == "NOT_FOUND" ? 404 : ex.Code == "VALIDATION_ERROR" ? 400 : 409, ApiResponse<object>.Fail(ex.Message, [ex.Code]));
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(ApiResponse<LiveSupportAdminDashboardDto>.Ok(await service.GetAdminDashboardAsync(ct)));

    [HttpGet("conversations/{conversationId:guid}/timeline")]
    public async Task<IActionResult> Timeline(Guid conversationId, CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportConversationTimelineDto>.Ok(await service.GetAdminTimelineAsync(conversationId, ct))); }
        catch (LiveSupportException ex) { return NotFound(ApiResponse<object>.Fail(ex.Message, [ex.Code])); }
    }

    [HttpPost("conversations/{conversationId:guid}/intervene")]
    public async Task<IActionResult> Intervene(Guid conversationId, AdminInterventionRequest request, CancellationToken ct)
    {
        try
        {
            var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(ApiResponse<LiveSupportConversationDto>.Ok(await service.AdminInterveneAsync(actor, conversationId, request.Operation, request.TargetStaffUserId, request.Reason, ct)));
        }
        catch (LiveSupportException ex) { return StatusCode(ex.Code == "NOT_FOUND" ? 404 : 409, ApiResponse<object>.Fail(ex.Message, [ex.Code])); }
    }
}

public sealed record UpdateFeatureRequest(bool Enabled);
public sealed record UpdateStaffConfigRequest(bool Enabled, int Capacity, long? ExpectedVersion, IReadOnlyList<LiveSupportScheduleWindowDto> Schedule);
public sealed record AdminInterventionRequest(string Operation, Guid? TargetStaffUserId, string Reason);
