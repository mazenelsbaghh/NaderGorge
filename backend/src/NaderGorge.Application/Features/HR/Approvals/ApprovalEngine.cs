using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Approvals;

public sealed class ApprovalEngine
{
    private readonly IAppDbContext _db;
    public ApprovalEngine(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApprovalInboxEntry>> GetInboxAsync(Guid actorUserId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var delegatedUserIds = await _db.ApprovalDelegations.AsNoTracking()
            .Where(delegation => delegation.DelegateUserId == actorUserId && delegation.IsActive &&
                delegation.StartsAt <= now && delegation.EndsAt >= now)
            .Select(delegation => delegation.PrincipalUserId)
            .ToListAsync(ct);
        var access = await GetApprovalAccessAsync(actorUserId, ct);
        var eligibleSteps = await _db.ApprovalStepInstances.AsNoTracking()
            .Where(step => step.State == ApprovalStepState.Pending &&
                step.Order == step.ApprovalInstance!.CurrentStepOrder &&
                step.ApprovalInstance.State == ApprovalInstanceState.Pending &&
                (step.OriginalApproverUserId == actorUserId ||
                 (step.OriginalApproverUserId.HasValue && delegatedUserIds.Contains(step.OriginalApproverUserId.Value)) ||
                 (step.OriginalApproverUserId == null &&
                  (access.IsAdmin ||
                   (step.EscalationLevel == 0 && access.Permissions.Contains(step.DefinitionStep!.Permission!)) ||
                   (step.EscalationLevel > 0 && access.Permissions.Contains(step.DefinitionStep!.EscalationPermission!))))))
            .OrderBy(step => step.DueAt)
            .Select(step => new ApprovalInboxEntry(step.Id, step.ApprovalInstanceId, step.Order, step.DueAt,
                step.EscalationLevel, step.ApprovalInstance!.RequestType, step.ApprovalInstance.RequestId,
                step.ApprovalInstance.Version, step.ApprovalInstance.RequesterEmployee!.User!.FullName,
                step.DefinitionStep!.Name, step.OriginalApproverUserId, step.DefinitionStep.Permission,
                step.DefinitionStep.EscalationPermission))
            .Take(500)
            .ToListAsync(ct);

        var leaveRequestIds = eligibleSteps.Where(step => step.RequestType == "leave")
            .Select(step => step.RequestId).Distinct().ToList();
        if (leaveRequestIds.Count == 0) return eligibleSteps;

        var leaveFacts = await _db.HrLeaveRequests.AsNoTracking()
            .Where(request => leaveRequestIds.Contains(request.Id))
            .Select(request => new
            {
                request.Id,
                LeaveType = request.LeaveType!.Name,
                request.StartDate,
                request.EndDate,
                request.DayFraction,
                request.Workdays,
                request.Reason,
                AvailableLeaveBalance = _db.LeaveBalances
                    .Where(balance => balance.EmployeeId == request.EmployeeId &&
                        balance.LeaveTypeId == request.LeaveTypeId && balance.Year == request.StartDate.Year)
                    .Select(balance => (decimal?)(balance.Granted + balance.Carried - balance.Reserved - balance.Used))
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(request => request.Id, ct);

        return eligibleSteps.Select(step => leaveFacts.TryGetValue(step.RequestId, out var leave)
            ? step with
            {
                LeaveType = leave.LeaveType,
                StartDate = leave.StartDate,
                EndDate = leave.EndDate,
                DayFraction = leave.DayFraction,
                Workdays = leave.Workdays,
                Reason = leave.Reason,
                AvailableLeaveBalance = leave.AvailableLeaveBalance
            }
            : step).ToList();
    }

    public async Task<ApiResponse<Guid>> StartAsync(string requestType, Guid requestId, Guid requesterEmployeeId, CancellationToken ct)
    {
        var existing = await _db.ApprovalInstances.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestType == requestType && item.RequestId == requestId, ct);
        if (existing is not null) return ApiResponse<Guid>.Ok(existing.Id);
        var definition = await _db.ApprovalDefinitions.Include(item => item.Steps)
            .Where(item => item.RequestType == requestType && item.IsActive)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(ct);
        if (definition is null || definition.Steps.Count == 0)
            return ApiResponse<Guid>.Fail("لا يوجد مسار موافقات فعال", ["APPROVAL_DEFINITION_NOT_FOUND"]);
        var now = DateTime.UtcNow;
        var instance = new ApprovalInstance
        {
            ApprovalDefinitionId = definition.Id, RequestType = requestType, RequestId = requestId,
            RequesterEmployeeId = requesterEmployeeId, CurrentStepOrder = definition.Steps.Min(item => item.Order)
        };
        foreach (var definitionStep in definition.Steps.OrderBy(item => item.Order))
        {
            var approverUserId = await ResolveOriginalApproverAsync(definitionStep, requesterEmployeeId, ct);
            if (!IsResolvable(definitionStep, approverUserId))
                return ApiResponse<Guid>.Fail("إحدى خطوات الموافقة بلا مسؤول صالح", ["APPROVER_NOT_FOUND"]);
            instance.Steps.Add(new ApprovalStepInstance
            {
                ApprovalDefinitionStepId = definitionStep.Id, Order = definitionStep.Order,
                OriginalApproverUserId = approverUserId,
                DueAt = now.AddMinutes(Math.Max(1, definitionStep.SlaMinutes))
            });
        }
        _db.ApprovalInstances.Add(instance);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(instance.Id);
    }

    public async Task<ApiResponse<bool>> DecideAsync(Guid instanceId, Guid actorUserId, bool approve, string reason,
        int expectedVersion, CancellationToken ct)
    {
        var instance = await _db.ApprovalInstances.Include(item => item.RequesterEmployee)
            .Include(item => item.Steps).ThenInclude(item => item.DefinitionStep)
            .SingleOrDefaultAsync(item => item.Id == instanceId, ct);
        if (instance is null) return ApiResponse<bool>.Fail("مسار الموافقة غير موجود", ["APPROVAL_NOT_FOUND"]);
        if (instance.State != ApprovalInstanceState.Pending) return ApiResponse<bool>.Ok(true);
        if (instance.Version != expectedVersion) return ApiResponse<bool>.Fail("تم تعديل مسار الموافقة", ["CONCURRENCY_CONFLICT"]);
        if (instance.RequesterEmployee?.UserId == actorUserId)
            return ApiResponse<bool>.Fail("لا يمكن اعتماد طلبك", ["SELF_APPROVAL_FORBIDDEN"]);
        var step = instance.Steps.Single(item => item.Order == instance.CurrentStepOrder);
        var delegation = step.OriginalApproverUserId.HasValue
            ? await _db.ApprovalDelegations.SingleOrDefaultAsync(item => item.PrincipalUserId == step.OriginalApproverUserId &&
                item.DelegateUserId == actorUserId && item.IsActive && item.Scope == instance.RequestType &&
                item.StartsAt <= DateTime.UtcNow && item.EndsAt >= DateTime.UtcNow, ct)
            : null;
        var requiredPermission = step.EscalationLevel > 0
            ? step.DefinitionStep?.EscalationPermission ?? step.DefinitionStep?.Permission
            : step.DefinitionStep?.Permission;
        var directlyEligible = step.OriginalApproverUserId == actorUserId ||
            (step.OriginalApproverUserId is null && await HasPermissionAsync(actorUserId, requiredPermission, ct));
        if (!directlyEligible && delegation is null)
            return ApiResponse<bool>.Fail("المستخدم غير مخول بهذه الخطوة", ["APPROVER_NOT_ELIGIBLE"]);

        step.ActingUserId = actorUserId; step.DelegationId = delegation?.Id; step.DecisionReason = reason.Trim();
        step.DecidedAt = DateTime.UtcNow; step.State = approve ? ApprovalStepState.Approved : ApprovalStepState.Rejected; step.Version++;
        instance.Version++;
        if (!approve) instance.State = ApprovalInstanceState.Rejected;
        else
        {
            var next = instance.Steps.Where(item => item.Order > step.Order).OrderBy(item => item.Order).FirstOrDefault();
            if (next is null) instance.State = ApprovalInstanceState.Approved;
            else instance.CurrentStepOrder = next.Order;
        }
        await _db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<int> EscalateDueAsync(DateTime now, CancellationToken ct)
    {
        var due = await _db.ApprovalStepInstances.Include(item => item.ApprovalInstance)
            .Where(item => item.State == ApprovalStepState.Pending && item.DueAt <= now &&
                item.ApprovalInstance!.State == ApprovalInstanceState.Pending && item.Order == item.ApprovalInstance.CurrentStepOrder)
            .ToListAsync(ct);
        var changed = 0;
        foreach (var step in due)
        {
            var key = $"{step.ApprovalInstanceId:N}:{step.Order}:{step.EscalationLevel + 1}";
            if (await _db.HrIdempotencyRecords.AnyAsync(item => item.Scope == "approval-escalation" && item.Key == key, ct)) continue;
            var previousApprover = step.OriginalApproverUserId;
            if (previousApprover.HasValue)
            {
                var managerEmployeeId = await _db.EmployeeProfiles.Where(item => item.UserId == previousApprover)
                    .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct);
                if (managerEmployeeId.HasValue)
                {
                    var today = CairoTime.ToDate(now);
                    step.OriginalApproverUserId = await _db.EmploymentAssignments
                        .Where(item => item.EmployeeId == managerEmployeeId && item.EffectiveFrom <= today &&
                            (!item.EffectiveTo.HasValue || item.EffectiveTo >= today) && item.ManagerEmployeeId.HasValue)
                        .OrderByDescending(item => item.EffectiveFrom).Select(item => (Guid?)item.ManagerEmployee!.UserId).FirstOrDefaultAsync(ct);
                }
            }
            if (step.OriginalApproverUserId == previousApprover &&
                !string.IsNullOrWhiteSpace(step.DefinitionStep?.EscalationPermission))
                step.OriginalApproverUserId = null;
            step.EscalationLevel++; step.DueAt = now.AddHours(24); step.Version++;
            _db.HrIdempotencyRecords.Add(new HrIdempotencyRecord { Scope = "approval-escalation", ActorUserId = Guid.Empty,
                Key = key, RequestHash = key, ResultEntityId = step.Id, ExpiresAt = now.AddYears(2) });
            _db.OutboxEvents.Add(new OutboxEvent { Type = "hr.approval.escalated", TargetUserId = step.OriginalApproverUserId?.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { step.ApprovalInstanceId, step.Order, step.EscalationLevel, previousApprover, step.OriginalApproverUserId }) });
            changed++;
        }
        if (changed > 0) await _db.SaveChangesAsync(ct);
        return changed;
    }

    private async Task<Guid?> ResolveOriginalApproverAsync(ApprovalDefinitionStep step, Guid requesterEmployeeId, CancellationToken ct)
    {
        if (step.ApproverKind == ApprovalApproverKind.SpecificUser) return step.SpecificUserId;
        if (step.ApproverKind == ApprovalApproverKind.Permission) return null;
        var today = CairoTime.GetCurrentDate();
        return await _db.EmploymentAssignments.Where(item => item.EmployeeId == requesterEmployeeId &&
                item.EffectiveFrom <= today && (!item.EffectiveTo.HasValue || item.EffectiveTo >= today) && item.ManagerEmployeeId.HasValue)
            .OrderByDescending(item => item.EffectiveFrom).Select(item => (Guid?)item.ManagerEmployee!.UserId).FirstOrDefaultAsync(ct);
    }

    private static bool IsResolvable(ApprovalDefinitionStep step, Guid? approverUserId) =>
        step.ApproverKind switch
        {
            ApprovalApproverKind.Permission => !string.IsNullOrWhiteSpace(step.Permission),
            _ => approverUserId.HasValue
        };

    private async Task<bool> HasPermissionAsync(Guid actorUserId, string? permission, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;
        var access = await GetApprovalAccessAsync(actorUserId, ct);
        return access.IsAdmin || access.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ApprovalAccess> GetApprovalAccessAsync(Guid actorUserId, CancellationToken ct)
    {
        var roles = await _db.UserRoles.AsNoTracking().Where(role => role.UserId == actorUserId)
            .Select(role => new { role.Role.Type, role.Role.PermissionsJson }).ToListAsync(ct);
        return new ApprovalAccess(
            roles.Any(role => role.Type == RoleType.Admin),
            roles.SelectMany(role => ParsePermissions(role.PermissionsJson))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IEnumerable<string> ParsePermissions(string? json)
    {
        string[] values;
        try { values = JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? []; }
        catch (JsonException) { yield break; }
        foreach (var permission in values)
            yield return permission.Split('@', 2, StringSplitOptions.TrimEntries)[0];
    }
}

public sealed record ApprovalInboxEntry(Guid Id, Guid ApprovalInstanceId, int Order, DateTime DueAt,
    int EscalationLevel, string RequestType, Guid RequestId, int InstanceVersion, string Requester,
    string Step, Guid? OriginalApproverUserId, string? Permission, string? EscalationPermission)
{
    // Populated only for leave requests, preserving nullable facts for all other approval types.
    public string? LeaveType { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public decimal? DayFraction { get; init; }
    public decimal? Workdays { get; init; }
    public string? Reason { get; init; }
    public decimal? AvailableLeaveBalance { get; init; }
}

internal sealed record ApprovalAccess(bool IsAdmin, string[] Permissions);
