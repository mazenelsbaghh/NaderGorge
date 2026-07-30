using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Approvals;
using NaderGorge.Application.Features.HR.Leave;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr"), Authorize]
public sealed class HrLeaveController(IAppDbContext db, LeaveRequestService leaveService, ApprovalEngine approvalEngine) : ControllerBase
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
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await leaveService.SubmitAsync(User.RequireUserId(), request.LeaveTypeId, request.StartDate, request.EndDate,
            request.DayFraction, request.Reason, request.AttachmentReference, ct);
        if (!result.Success) return BadRequest(result);
        var leave = await db.HrLeaveRequests.SingleAsync(item => item.Id == result.Data, ct);
        var approval = await approvalEngine.StartAsync("leave", leave.Id, leave.EmployeeId, ct);
        if (!approval.Success) return Conflict(approval);
        leave.ApprovalInstanceId = approval.Data;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(result);
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
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (code.Length is < 2 or > 50 || name.Length is < 2 or > 200) return BadRequest(new { errors = new[] { "LEAVE_TYPE_INVALID" } });
        if (await db.LeaveTypes.AnyAsync(item => item.Code == code, ct)) return Conflict(new { errors = new[] { "LEAVE_TYPE_CODE_EXISTS" } });
        var type = new LeaveType { Code = code, Name = name, IsPaid = request.IsPaid,
            RequiresAttachment = request.RequiresAttachment, AllowsHalfDay = request.AllowsHalfDay };
        db.LeaveTypes.Add(type); await db.SaveChangesAsync(ct); return Ok(new { type.Id });
    }

    [HttpPost("admin/leave/policies"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> CreatePolicy(CreateLeavePolicyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.AnnualEntitlement < 0 || request.MaximumCarryover < 0 ||
            request.EffectiveTo < request.EffectiveFrom) return BadRequest(new { errors = new[] { "LEAVE_POLICY_INVALID" } });
        if (!await db.LeaveTypes.AnyAsync(item => item.Id == request.LeaveTypeId && item.IsActive, ct) ||
            !await db.WorkCalendars.AnyAsync(item => item.Id == request.WorkCalendarId, ct))
            return BadRequest(new { errors = new[] { "LEAVE_POLICY_REFERENCE_INVALID" } });
        var overlaps = await db.LeavePolicies.AnyAsync(item => item.LeaveTypeId == request.LeaveTypeId &&
            item.EffectiveFrom <= (request.EffectiveTo ?? DateOnly.MaxValue) &&
            (!item.EffectiveTo.HasValue || item.EffectiveTo >= request.EffectiveFrom), ct);
        if (overlaps) return Conflict(new { errors = new[] { "LEAVE_POLICY_OVERLAP" } });
        var policy = new LeavePolicy { Name = request.Name.Trim(), LeaveTypeId = request.LeaveTypeId,
            AnnualEntitlement = request.AnnualEntitlement, MaximumCarryover = request.MaximumCarryover,
            AllowNegativeBalance = request.AllowNegativeBalance, EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo, WorkCalendarId = request.WorkCalendarId };
        db.LeavePolicies.Add(policy); await db.SaveChangesAsync(ct); return Ok(new { policy.Id });
    }

    [HttpPost("admin/leave/balances/grant"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> GrantBalance(GrantLeaveBalanceRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Year is < 2000 or > 2200)
            return BadRequest(new { errors = new[] { "LEAVE_GRANT_INVALID" } });
        if (!await db.EmployeeProfiles.AnyAsync(item => item.Id == request.EmployeeId, ct) ||
            !await db.LeaveTypes.AnyAsync(item => item.Id == request.LeaveTypeId && item.IsActive, ct))
            return BadRequest(new { errors = new[] { "LEAVE_GRANT_REFERENCE_INVALID" } });
        var balance = await db.LeaveBalances.SingleOrDefaultAsync(item => item.EmployeeId == request.EmployeeId && item.LeaveTypeId == request.LeaveTypeId && item.Year == request.Year, ct);
        if (balance is null) { balance = new LeaveBalance { EmployeeId = request.EmployeeId, LeaveTypeId = request.LeaveTypeId, Year = request.Year }; db.LeaveBalances.Add(balance); }
        balance.Granted += request.Amount; balance.Version++;
        db.LeaveLedgerEntries.Add(new LeaveLedgerEntry { LeaveBalanceId = balance.Id, EntryType = LeaveLedgerEntryType.Grant,
            Amount = request.Amount, SourceType = "AdminGrant", SourceId = Guid.NewGuid(), Reason = request.Reason.Trim(), ActorUserId = User.RequireUserId() });
        await db.SaveChangesAsync(ct); return Ok(new { balance.Id, balance.Available });
    }
}

public sealed record SubmitLeaveRequest(Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, decimal DayFraction, string Reason, string? AttachmentReference);
public sealed record WithdrawLeaveRequest(string Reason);
public sealed record CreateLeaveTypeRequest(string Code, string Name, bool IsPaid, bool RequiresAttachment, bool AllowsHalfDay);
public sealed record CreateLeavePolicyRequest(string Name, Guid LeaveTypeId, decimal AnnualEntitlement, decimal MaximumCarryover,
    bool AllowNegativeBalance, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid WorkCalendarId);
public sealed record GrantLeaveBalanceRequest(Guid EmployeeId, Guid LeaveTypeId, int Year, decimal Amount, string Reason);
