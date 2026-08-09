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
[Authorize]
[HasPermission("payments.manage")]
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

    [HttpGet("sms-logs")]
    public async Task<IActionResult> GetSmsLogs(
        [FromQuery] string? search,
        [FromQuery] bool? isMatched,
        [FromQuery] Guid? walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetWalletSmsLogsQuery(search, isMatched, walletId, page, pageSize), ct));

    [HttpGet("recharge-requests/{id:guid}/sms-suggestions")]
    public async Task<IActionResult> GetRechargeSmsSuggestions(
        [FromRoute] Guid id,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRechargeSmsSuggestionsQuery(id, search), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("recharge-message-conflicts")]
    public async Task<IActionResult> GetRechargeMessageConflicts(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRechargeMessageConflictsQuery(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("recharge-requests/{id:guid}/reassign-sms")]
    public async Task<IActionResult> ReassignRechargeSms(
        [FromRoute] Guid id,
        [FromBody] ReassignRechargeSmsRequestDto dto,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ReassignRechargeSmsCommand(id, dto.SmsLogId, User.RequireUserId(), dto.Reason), ct);
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
            dto.SmsLogId,
            dto.WalletId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("recharge-shift-review")]
    public async Task<IActionResult> GetRechargeShiftReview(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? walletId,
        [FromQuery] Guid? resolvedByUserId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRechargeShiftReviewQuery(from, to, walletId, resolvedByUserId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("recharge-requests/{id:guid}/reverse-credit")]
    public async Task<IActionResult> ReverseRechargeCredit(
        [FromRoute] Guid id,
        [FromBody] ReverseRechargeCreditRequestDto dto,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ReverseRechargeCreditCommand(id, User.RequireUserId(), dto.Reason), ct);
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
    Guid? SmsLogId,
    Guid? WalletId);

public record ReverseRechargeCreditRequestDto(string Reason);

public record ReassignRechargeSmsRequestDto(Guid SmsLogId, string Reason);
