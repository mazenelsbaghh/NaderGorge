using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Approvals;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Leave;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr"), Authorize]
public sealed class HrLeaveController(IAppDbContext db, LeaveRequestService leaveService, IMediator mediator) : ControllerBase
{
    [HttpGet("self/leave/catalog"), HasPermission(HrPermissions.LeaveSelf)]
    public async Task<IActionResult> Catalog(CancellationToken ct) => Ok(await db.LeaveTypes.AsNoTracking().Where(item => item.IsActive)
        .OrderBy(item => item.Name).Select(item => new { item.Id, item.Code, item.Name, item.IsPaid, item.RequiresAttachment, item.AllowsHalfDay }).ToListAsync(ct));

    [HttpGet("self/leave/balances"), HasPermission(HrPermissions.LeaveSelf)]
    public async Task<IActionResult> Balances(CancellationToken ct)
    {
        var userId = User.RequireUserId();
        return Ok(await db.LeaveBalances.AsNoTracking().Where(item => item.Employee!.UserId == userId)
            .OrderByDescending(item => item.Year).ThenBy(item => item.LeaveType!.Name)
            .Select(item => new { item.Id, item.LeaveTypeId, leaveType = item.LeaveType!.Name, item.Year, item.Granted, item.Carried, item.Reserved, item.Used,
                available = item.Granted + item.Carried - item.Reserved - item.Used }).ToListAsync(ct));
    }

    [HttpGet("self/leave/requests"), HasPermission(HrPermissions.LeaveSelf)]
    public async Task<IActionResult> MyRequests(CancellationToken ct)
    {
        var userId = User.RequireUserId();
        return Ok(await db.HrLeaveRequests.AsNoTracking().Where(item => item.Employee!.UserId == userId).OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, item.LeaveTypeId, leaveType = item.LeaveType!.Name, item.StartDate, item.EndDate, item.DayFraction,
                item.Workdays, item.Reason, item.AttachmentReference, state = item.State.ToString(), item.ApprovalInstanceId, item.Version }).ToListAsync(ct));
    }

    [HttpPost("self/leave/requests"), HasPermission(HrPermissions.LeaveSelf)]
    public async Task<IActionResult> Submit(SubmitLeaveRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SubmitLeaveWithApprovalCommand(User.RequireUserId(), request.LeaveTypeId,
            request.StartDate, request.EndDate, request.DayFraction, request.Reason, request.AttachmentReference), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("self/leave/requests/{requestId:guid}/withdraw"), HasPermission(HrPermissions.LeaveSelf)]
    public async Task<IActionResult> Withdraw(Guid requestId, WithdrawLeaveRequest request, CancellationToken ct)
    {
        var result = await leaveService.WithdrawAsync(User.RequireUserId(), requestId, request.Reason, ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpGet("admin/leave/requests"), HasPermission(HrPermissions.LeaveTeamReview)]
    public async Task<IActionResult> Queue(CancellationToken ct) => Ok(await db.HrLeaveRequests.AsNoTracking().OrderByDescending(item => item.CreatedAt)
        .Select(item => new { item.Id, item.EmployeeId, employee = item.Employee!.User!.FullName, leaveType = item.LeaveType!.Name,
            item.StartDate, item.EndDate, item.Workdays, item.Reason, state = item.State.ToString(), item.ApprovalInstanceId, item.Version }).Take(200).ToListAsync(ct));

    [HttpGet("admin/leave/config"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> Configuration(CancellationToken ct) => Ok(new
    {
        types = await db.LeaveTypes.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct),
        policies = await db.LeavePolicies.AsNoTracking().OrderByDescending(item => item.EffectiveFrom)
            .Select(item => new { item.Id, item.Name, item.LeaveTypeId, leaveType = item.LeaveType!.Name, item.AnnualEntitlement,
                item.MaximumCarryover, item.AllowNegativeBalance, item.EffectiveFrom, item.EffectiveTo, item.WorkCalendarId }).ToListAsync(ct)
    });

    [HttpPost("admin/leave/types"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> CreateType(CreateLeaveTypeRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateLeaveTypeCommand(request.Code, request.Name, request.IsPaid,
            request.RequiresAttachment, request.AllowsHalfDay), ct);
        return result.Success ? Ok(new { Id = result.Data }) : result.Errors?.Contains("LEAVE_TYPE_CODE_EXISTS") == true ? Conflict(result) : BadRequest(result);
    }

    [HttpPost("admin/leave/policies"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> CreatePolicy(CreateLeavePolicyRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateLeavePolicyCommand(request.Name, request.LeaveTypeId,
            request.AnnualEntitlement, request.MaximumCarryover, request.AllowNegativeBalance, request.EffectiveFrom,
            request.EffectiveTo, request.WorkCalendarId), ct);
        return result.Success ? Ok(new { Id = result.Data }) : result.Errors?.Contains("LEAVE_POLICY_OVERLAP") == true ? Conflict(result) : BadRequest(result);
    }

    [HttpPost("admin/leave/balances/grant"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> GrantBalance(GrantLeaveBalanceRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new GrantLeaveBalanceCommand(User.RequireUserId(), request.EmployeeId,
            request.LeaveTypeId, request.Year, request.Amount, request.Reason), ct);
        return result.Success ? Ok(new { Id = result.Data, Available = decimal.Parse(result.Message!) }) : BadRequest(result);
    }
}

public sealed record SubmitLeaveRequest(Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, decimal DayFraction, string Reason, string? AttachmentReference);
public sealed record WithdrawLeaveRequest(string Reason);
public sealed record CreateLeaveTypeRequest(string Code, string Name, bool IsPaid, bool RequiresAttachment, bool AllowsHalfDay);
public sealed record CreateLeavePolicyRequest(string Name, Guid LeaveTypeId, decimal AnnualEntitlement, decimal MaximumCarryover,
    bool AllowNegativeBalance, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid WorkCalendarId);
public sealed record GrantLeaveBalanceRequest(Guid EmployeeId, Guid LeaveTypeId, int Year, decimal Amount, string Reason);
