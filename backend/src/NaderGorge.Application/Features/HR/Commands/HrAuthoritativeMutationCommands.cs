using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Approvals;
using NaderGorge.Application.Features.HR.Leave;
using NaderGorge.Application.Features.HR.Payroll;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public sealed record ApprovalStepInput(int Order, string Name, ApprovalApproverKind ApproverKind, string? Permission,
    Guid? SpecificUserId, int SlaMinutes, string? EscalationPermission);
public sealed record CreateApprovalDelegationCommand(Guid PrincipalUserId, Guid DelegateUserId, string Scope,
    DateTime StartsAt, DateTime EndsAt, string Reason) : IRequest<ApiResponse<Guid>>;
public sealed record CreateApprovalDefinitionCommand(string RequestType, string Name, IReadOnlyList<ApprovalStepInput> Steps)
    : IRequest<ApiResponse<Guid>>;
public sealed record SubmitLeaveWithApprovalCommand(Guid ActorUserId, Guid LeaveTypeId, DateOnly StartDate,
    DateOnly EndDate, decimal DayFraction, string Reason, string? AttachmentReference) : IRequest<ApiResponse<Guid>>;
public sealed record CreateLeaveTypeCommand(string Code, string Name, bool IsPaid, bool RequiresAttachment, bool AllowsHalfDay)
    : IRequest<ApiResponse<Guid>>;
public sealed record CreateLeavePolicyCommand(string Name, Guid LeaveTypeId, decimal AnnualEntitlement,
    decimal MaximumCarryover, bool AllowNegativeBalance, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    Guid WorkCalendarId) : IRequest<ApiResponse<Guid>>;
public sealed record GrantLeaveBalanceCommand(Guid ActorUserId, Guid EmployeeId, Guid LeaveTypeId, int Year,
    decimal Amount, string Reason) : IRequest<ApiResponse<Guid>>;
public sealed record DecideApprovalCommand(Guid InstanceId, Guid ActorUserId, bool Approve, string Reason,
    int ExpectedVersion) : IRequest<ApiResponse<bool>>;

public sealed class HrApprovalLeaveMutationHandler(IAppDbContext db, LeaveRequestService leaveService, ApprovalEngine approvalEngine) :
    IRequestHandler<CreateApprovalDelegationCommand, ApiResponse<Guid>>,
    IRequestHandler<CreateApprovalDefinitionCommand, ApiResponse<Guid>>,
    IRequestHandler<SubmitLeaveWithApprovalCommand, ApiResponse<Guid>>,
    IRequestHandler<CreateLeaveTypeCommand, ApiResponse<Guid>>,
    IRequestHandler<CreateLeavePolicyCommand, ApiResponse<Guid>>,
    IRequestHandler<GrantLeaveBalanceCommand, ApiResponse<Guid>>,
    IRequestHandler<DecideApprovalCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DecideApprovalCommand request, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var decision = await approvalEngine.DecideAsync(request.InstanceId, request.ActorUserId, request.Approve,
                request.Reason, request.ExpectedVersion, ct);
            if (!decision.Success) return decision;
            var instance = await db.ApprovalInstances.AsNoTracking().SingleAsync(item => item.Id == request.InstanceId, ct);
            if (instance.RequestType == "leave" && instance.State != ApprovalInstanceState.Pending)
            {
                var finalization = instance.State == ApprovalInstanceState.Approved
                    ? await leaveService.FinalizeApprovedAsync(instance.RequestId, request.ActorUserId, ct)
                    : await leaveService.FinalizeRejectedAsync(instance.RequestId, request.ActorUserId, request.Reason, ct);
                if (!finalization.Success) return finalization;
            }
            await transaction.CommitAsync(ct);
            return decision;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<bool>.Fail("تم تعديل مسار الموافقة", ["CONCURRENCY_CONFLICT"]);
        }
    }

    public async Task<ApiResponse<Guid>> Handle(CreateApprovalDelegationCommand request, CancellationToken ct)
    {
        if (request.EndsAt <= request.StartsAt || request.PrincipalUserId == request.DelegateUserId ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Scope != "leave")
            return ApiResponse<Guid>.Fail("Invalid delegation", ["DELEGATION_INVALID"]);
        if (!await db.EmployeeProfiles.AnyAsync(employee => employee.UserId == request.DelegateUserId, ct))
            return ApiResponse<Guid>.Fail("Delegate is not an employee", ["DELEGATE_NOT_EMPLOYEE"]);
        var row = new ApprovalDelegation { PrincipalUserId = request.PrincipalUserId, DelegateUserId = request.DelegateUserId,
            Scope = request.Scope, StartsAt = request.StartsAt, EndsAt = request.EndsAt, Reason = request.Reason.Trim() };
        db.ApprovalDelegations.Add(row);
        await db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(row.Id);
    }

    public async Task<ApiResponse<Guid>> Handle(CreateApprovalDefinitionCommand request, CancellationToken ct)
    {
        if (request.RequestType != "leave" || request.Steps.Count == 0 ||
            !request.Steps.Select(step => step.Order).Order().SequenceEqual(Enumerable.Range(1, request.Steps.Count)) ||
            request.Steps.Any(step => step.Order <= 0 || step.SlaMinutes <= 0 || string.IsNullOrWhiteSpace(step.Name) ||
                step.ApproverKind switch { ApprovalApproverKind.DirectManager => false,
                    ApprovalApproverKind.Permission => string.IsNullOrWhiteSpace(step.Permission),
                    ApprovalApproverKind.SpecificUser => !step.SpecificUserId.HasValue, _ => true }))
            return ApiResponse<Guid>.Fail("Invalid approval definition", ["APPROVAL_DEFINITION_INVALID"]);
        var users = request.Steps.Where(step => step.ApproverKind == ApprovalApproverKind.SpecificUser)
            .Select(step => step.SpecificUserId!.Value).Distinct().ToList();
        if (users.Count > 0 && await db.Users.CountAsync(user => users.Contains(user.Id), ct) != users.Count)
            return ApiResponse<Guid>.Fail("Approver not found", ["APPROVER_NOT_FOUND"]);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var version = (await db.ApprovalDefinitions.Where(item => item.RequestType == request.RequestType)
            .Select(item => (int?)item.Version).MaxAsync(ct) ?? 0) + 1;
        foreach (var old in await db.ApprovalDefinitions.Where(item => item.RequestType == request.RequestType && item.IsActive).ToListAsync(ct))
            old.IsActive = false;
        var definition = new ApprovalDefinition { RequestType = request.RequestType.Trim(), Name = request.Name.Trim(), Version = version };
        foreach (var step in request.Steps.OrderBy(item => item.Order)) definition.Steps.Add(new ApprovalDefinitionStep
        {
            ApprovalDefinitionId = definition.Id, Order = step.Order, Name = step.Name.Trim(), ApproverKind = step.ApproverKind,
            Permission = step.Permission, SpecificUserId = step.SpecificUserId, SlaMinutes = step.SlaMinutes,
            EscalationPermission = step.EscalationPermission
        });
        db.ApprovalDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ApiResponse<Guid>.Ok(definition.Id, version.ToString());
    }

    public async Task<ApiResponse<Guid>> Handle(SubmitLeaveWithApprovalCommand request, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await leaveService.SubmitAsync(request.ActorUserId, request.LeaveTypeId, request.StartDate,
            request.EndDate, request.DayFraction, request.Reason, request.AttachmentReference, ct);
        if (!result.Success) return result;
        var leave = await db.HrLeaveRequests.SingleAsync(item => item.Id == result.Data, ct);
        var approval = await approvalEngine.StartAsync("leave", leave.Id, leave.EmployeeId, ct);
        if (!approval.Success) return ApiResponse<Guid>.Fail(approval.Message ?? "Approval failed", approval.Errors);
        leave.ApprovalInstanceId = approval.Data;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateLeaveTypeCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant(); var name = request.Name.Trim();
        if (code.Length is < 2 or > 50 || name.Length is < 2 or > 200)
            return ApiResponse<Guid>.Fail("Invalid leave type", ["LEAVE_TYPE_INVALID"]);
        if (await db.LeaveTypes.AnyAsync(item => item.Code == code, ct))
            return ApiResponse<Guid>.Fail("Code exists", ["LEAVE_TYPE_CODE_EXISTS"]);
        var row = new LeaveType { Code = code, Name = name, IsPaid = request.IsPaid,
            RequiresAttachment = request.RequiresAttachment, AllowsHalfDay = request.AllowsHalfDay };
        db.LeaveTypes.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }

    public async Task<ApiResponse<Guid>> Handle(CreateLeavePolicyCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.AnnualEntitlement < 0 || request.MaximumCarryover < 0 || request.EffectiveTo < request.EffectiveFrom)
            return ApiResponse<Guid>.Fail("Invalid policy", ["LEAVE_POLICY_INVALID"]);
        if (!await db.LeaveTypes.AnyAsync(item => item.Id == request.LeaveTypeId && item.IsActive, ct) ||
            !await db.WorkCalendars.AnyAsync(item => item.Id == request.WorkCalendarId, ct))
            return ApiResponse<Guid>.Fail("Invalid reference", ["LEAVE_POLICY_REFERENCE_INVALID"]);
        if (await db.LeavePolicies.AnyAsync(item => item.LeaveTypeId == request.LeaveTypeId &&
            item.EffectiveFrom <= (request.EffectiveTo ?? DateOnly.MaxValue) && (!item.EffectiveTo.HasValue || item.EffectiveTo >= request.EffectiveFrom), ct))
            return ApiResponse<Guid>.Fail("Overlapping policy", ["LEAVE_POLICY_OVERLAP"]);
        var row = new LeavePolicy { Name = request.Name.Trim(), LeaveTypeId = request.LeaveTypeId,
            AnnualEntitlement = request.AnnualEntitlement, MaximumCarryover = request.MaximumCarryover,
            AllowNegativeBalance = request.AllowNegativeBalance, EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo, WorkCalendarId = request.WorkCalendarId };
        db.LeavePolicies.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }

    public async Task<ApiResponse<Guid>> Handle(GrantLeaveBalanceCommand request, CancellationToken ct)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Year is < 2000 or > 2200)
            return ApiResponse<Guid>.Fail("Invalid grant", ["LEAVE_GRANT_INVALID"]);
        if (!await db.EmployeeProfiles.AnyAsync(item => item.Id == request.EmployeeId, ct) ||
            !await db.LeaveTypes.AnyAsync(item => item.Id == request.LeaveTypeId && item.IsActive, ct))
            return ApiResponse<Guid>.Fail("Invalid reference", ["LEAVE_GRANT_REFERENCE_INVALID"]);
        var balance = await db.LeaveBalances.SingleOrDefaultAsync(item => item.EmployeeId == request.EmployeeId && item.LeaveTypeId == request.LeaveTypeId && item.Year == request.Year, ct);
        if (balance is null) { balance = new LeaveBalance { EmployeeId = request.EmployeeId, LeaveTypeId = request.LeaveTypeId, Year = request.Year }; db.LeaveBalances.Add(balance); }
        balance.Granted += request.Amount; balance.Version++;
        db.LeaveLedgerEntries.Add(new LeaveLedgerEntry { LeaveBalanceId = balance.Id, EntryType = LeaveLedgerEntryType.Grant,
            Amount = request.Amount, SourceType = "AdminGrant", SourceId = Guid.NewGuid(), Reason = request.Reason.Trim(), ActorUserId = request.ActorUserId });
        await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(balance.Id, balance.Available.ToString());
    }
}
