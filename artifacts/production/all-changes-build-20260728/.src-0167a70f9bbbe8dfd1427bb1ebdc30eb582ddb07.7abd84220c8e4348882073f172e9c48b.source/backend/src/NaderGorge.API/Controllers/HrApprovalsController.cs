using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Approvals;
using NaderGorge.Application.Features.HR.Leave;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr/approvals"), Authorize]
public sealed class HrApprovalsController(IAppDbContext db, ApprovalEngine engine, LeaveRequestService leaveService) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox(CancellationToken ct)
        => Ok(await engine.GetInboxAsync(User.RequireUserId(), ct));

    [HttpPost("{instanceId:guid}/decision")]
    public async Task<IActionResult> Decide(Guid instanceId, ApprovalDecisionRequest request, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var actorUserId = User.RequireUserId();
            var decision = await engine.DecideAsync(instanceId, actorUserId, request.Approve, request.Reason, request.ExpectedVersion, ct);
            if (!decision.Success) return Conflict(decision);
            var instance = await db.ApprovalInstances.AsNoTracking().SingleAsync(approval => approval.Id == instanceId, ct);
            var finalization = await FinalizeRequestAsync(instance, actorUserId, request.Reason, ct);
            if (!finalization.Success) return Conflict(finalization);
            await transaction.CommitAsync(ct);
            return Ok(decision);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(ApiResponse<bool>.Fail("تم تعديل مسار الموافقة", ["CONCURRENCY_CONFLICT"]));
        }
    }

    [HttpGet("delegations"), HasPermission(HrPermissions.LeaveTeamReview)]
    public async Task<IActionResult> Delegations(CancellationToken ct) => Ok(await db.ApprovalDelegations.AsNoTracking()
        .Where(item => item.PrincipalUserId == User.RequireUserId()).OrderByDescending(item => item.StartsAt).ToListAsync(ct));

    [HttpPost("delegations"), HasPermission(HrPermissions.LeaveTeamReview)]
    public async Task<IActionResult> Delegate(CreateDelegationRequest request, CancellationToken ct)
    {
        var principalUserId = User.RequireUserId();
        if (request.EndsAt <= request.StartsAt || principalUserId == request.DelegateUserId ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Scope != "leave") return BadRequest();
        if (!await db.EmployeeProfiles.AnyAsync(employee => employee.UserId == request.DelegateUserId, ct))
            return BadRequest(new { errors = new[] { "DELEGATE_NOT_EMPLOYEE" } });
        var delegation = new ApprovalDelegation { PrincipalUserId = principalUserId, DelegateUserId = request.DelegateUserId,
            Scope = request.Scope, StartsAt = request.StartsAt, EndsAt = request.EndsAt, Reason = request.Reason.Trim() };
        db.ApprovalDelegations.Add(delegation); await db.SaveChangesAsync(ct); return Ok(new { delegation.Id });
    }

    [HttpPost("definitions"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> CreateDefinition(CreateApprovalDefinitionRequest request, CancellationToken ct)
    {
        if (request.RequestType != "leave" || request.Steps.Count == 0 ||
            request.Steps.Select(step => step.Order).Order().SequenceEqual(Enumerable.Range(1, request.Steps.Count)) == false ||
            request.Steps.Any(step => !IsValid(step))) return BadRequest();
        var specificUsers = request.Steps.Where(step => step.ApproverKind == ApprovalApproverKind.SpecificUser)
            .Select(step => step.SpecificUserId!.Value).Distinct().ToList();
        if (specificUsers.Count > 0 && await db.Users.CountAsync(user => specificUsers.Contains(user.Id), ct) != specificUsers.Count)
            return BadRequest(new { errors = new[] { "APPROVER_NOT_FOUND" } });
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var nextVersion = (await db.ApprovalDefinitions.Where(item => item.RequestType == request.RequestType)
            .Select(item => (int?)item.Version).MaxAsync(ct) ?? 0) + 1;
        var active = await db.ApprovalDefinitions.Where(item => item.RequestType == request.RequestType && item.IsActive).ToListAsync(ct);
        foreach (var old in active) old.IsActive = false;
        var definition = new ApprovalDefinition { RequestType = request.RequestType.Trim(), Name = request.Name.Trim(), Version = nextVersion };
        foreach (var step in request.Steps.OrderBy(item => item.Order)) definition.Steps.Add(new ApprovalDefinitionStep
        {
            ApprovalDefinitionId = definition.Id, Order = step.Order, Name = step.Name.Trim(), ApproverKind = step.ApproverKind,
            Permission = step.Permission, SpecificUserId = step.SpecificUserId, SlaMinutes = step.SlaMinutes,
            EscalationPermission = step.EscalationPermission
        });
        db.ApprovalDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { definition.Id, definition.Version });
    }

    private async Task<ApiResponse<bool>> FinalizeRequestAsync(ApprovalInstance instance, Guid actorUserId, string reason, CancellationToken ct)
    {
        if (instance.RequestType != "leave" || instance.State == ApprovalInstanceState.Pending)
            return ApiResponse<bool>.Ok(true);
        return instance.State == ApprovalInstanceState.Approved
            ? await leaveService.FinalizeApprovedAsync(instance.RequestId, actorUserId, ct)
            : await leaveService.FinalizeRejectedAsync(instance.RequestId, actorUserId, reason, ct);
    }

    private static bool IsValid(ApprovalStepRequest step) =>
        step.Order > 0 && step.SlaMinutes > 0 && !string.IsNullOrWhiteSpace(step.Name) &&
        (step.ApproverKind switch
        {
            ApprovalApproverKind.DirectManager => true,
            ApprovalApproverKind.Permission => !string.IsNullOrWhiteSpace(step.Permission),
            ApprovalApproverKind.SpecificUser => step.SpecificUserId.HasValue,
            _ => false
        });
}

public sealed record ApprovalDecisionRequest(bool Approve, string Reason, int ExpectedVersion);
public sealed record CreateDelegationRequest(Guid DelegateUserId, string Scope, DateTime StartsAt, DateTime EndsAt, string Reason);
public sealed record ApprovalStepRequest(int Order, string Name, ApprovalApproverKind ApproverKind, string? Permission, Guid? SpecificUserId,
    int SlaMinutes, string? EscalationPermission);
public sealed record CreateApprovalDefinitionRequest(string RequestType, string Name, IReadOnlyList<ApprovalStepRequest> Steps);
