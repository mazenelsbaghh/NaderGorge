using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Wallets;
using NaderGorge.Application.Features.Admin.Recharge;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/wallets")]
[Authorize(Roles = "Admin,Supervisor,Assistant")]
public class AdminWalletsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminWalletsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetWallets(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWalletsQuery(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWallet([FromBody] CreateWalletRequestDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateWalletCommand(
            dto.PhoneNumber,
            dto.Label,
            dto.DailyLimit,
            dto.MonthlyLimit,
            dto.SmsSenderFilters), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<IActionResult> ToggleWallet([FromRoute] Guid id, [FromBody] ToggleWalletRequestDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new ToggleWalletActiveCommand(id, dto.IsActive), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/regenerate-token")]
    public async Task<IActionResult> RegenerateToken([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new RegenerateWalletTokenCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}/limits")]
    public async Task<IActionResult> UpdateLimits([FromRoute] Guid id, [FromBody] UpdateWalletLimitsRequestDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateWalletLimitsCommand(
            id,
            dto.Label,
            dto.DailyLimit,
            dto.MonthlyLimit,
            dto.SmsSenderFilters), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("recharge-requests")]
    public async Task<IActionResult> GetRechargeRequests([FromQuery] RechargeRequestStatus? status, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAdminRechargeRequestsQuery(status), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("unmatched-sms")]
    public async Task<IActionResult> GetUnmatchedSms(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUnmatchedSmsLogsQuery(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("recharge-requests/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveRechargeRequest([FromRoute] Guid id, [FromBody] ResolveRechargeRequestDto dto, CancellationToken ct)
    {
        var adminId = User.RequireUserId();
        var result = await _mediator.Send(new ResolveRechargeRequestCommand(
            id,
            dto.Approve,
            adminId,
            dto.RejectionReason,
            dto.SmsLogId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record CreateWalletRequestDto(
    string PhoneNumber,
    string Label,
    decimal DailyLimit,
    decimal MonthlyLimit,
    List<string> SmsSenderFilters);

public record ToggleWalletRequestDto(bool IsActive);

public record UpdateWalletLimitsRequestDto(
    string Label,
    decimal DailyLimit,
    decimal MonthlyLimit,
    List<string> SmsSenderFilters);

public record ResolveRechargeRequestDto(
    bool Approve,
    string? RejectionReason,
    Guid? SmsLogId);

