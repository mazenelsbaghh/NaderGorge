using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Approvals;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Leave;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr/approvals"), Authorize]
public sealed class HrApprovalsController(IAppDbContext db, ApprovalEngine engine, IMediator mediator) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox(CancellationToken ct)
        => Ok(await engine.GetInboxAsync(User.RequireUserId(), ct));

    [HttpPost("{instanceId:guid}/decision")]
    public async Task<IActionResult> Decide(Guid instanceId, ApprovalDecisionRequest request, CancellationToken ct)
    {
        var decision = await mediator.Send(new DecideApprovalCommand(instanceId, User.RequireUserId(), request.Approve,
            request.Reason, request.ExpectedVersion), ct);
        return decision.Success ? Ok(decision) : Conflict(decision);
    }

    [HttpGet("delegations"), HasPermission(HrPermissions.LeaveTeamReview)]
    public async Task<IActionResult> Delegations(CancellationToken ct) => Ok(await db.ApprovalDelegations.AsNoTracking()
        .Where(item => item.PrincipalUserId == User.RequireUserId()).OrderByDescending(item => item.StartsAt).ToListAsync(ct));

    [HttpPost("delegations"), HasPermission(HrPermissions.LeaveTeamReview)]
    public async Task<IActionResult> Delegate(CreateDelegationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateApprovalDelegationCommand(User.RequireUserId(), request.DelegateUserId,
            request.Scope, request.StartsAt, request.EndsAt, request.Reason), ct);
        return result.Success ? Ok(new { Id = result.Data }) : BadRequest(result);
    }

    [HttpPost("definitions"), HasPermission(HrPermissions.LeaveManage)]
    public async Task<IActionResult> CreateDefinition(CreateApprovalDefinitionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateApprovalDefinitionCommand(request.RequestType, request.Name,
            request.Steps.Select(step => new ApprovalStepInput(step.Order, step.Name, step.ApproverKind, step.Permission,
                step.SpecificUserId, step.SlaMinutes, step.EscalationPermission)).ToList()), ct);
        return result.Success ? Ok(new { Id = result.Data, Version = int.Parse(result.Message!) }) : BadRequest(result);
    }

}

public sealed record ApprovalDecisionRequest(bool Approve, string Reason, int ExpectedVersion);
public sealed record CreateDelegationRequest(Guid DelegateUserId, string Scope, DateTime StartsAt, DateTime EndsAt, string Reason);
public sealed record ApprovalStepRequest(int Order, string Name, ApprovalApproverKind ApproverKind, string? Permission, Guid? SpecificUserId,
    int SlaMinutes, string? EscalationPermission);
public sealed record CreateApprovalDefinitionRequest(string RequestType, string Name, IReadOnlyList<ApprovalStepRequest> Steps);
