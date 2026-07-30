using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Scheduling.Commands;

public sealed record SubmitShiftSwapCommand(Guid RequesterEmployeeId, Guid RequesterAssignmentId, Guid TargetEmployeeId,
    Guid TargetAssignmentId, string Reason, Guid ActorUserId) : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.ShiftRead;
    public HrAccessScope RequiredScope => HrAccessScope.Self;
    public Guid? ResourceEmployeeId => RequesterEmployeeId;
}

public sealed class SubmitShiftSwapCommandHandler : IRequestHandler<SubmitShiftSwapCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit;
    public SubmitShiftSwapCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); }
    public async Task<ApiResponse<Guid>> Handle(SubmitShiftSwapCommand request, CancellationToken ct)
    {
        if (request.RequesterEmployeeId == request.TargetEmployeeId) return ApiResponse<Guid>.Fail("لا يمكن تبديل الشفت مع نفس الموظف", ["SHIFT_SWAP_SAME_EMPLOYEE"]);
        var assignments = await _db.ShiftAssignments.AsNoTracking().Where(item => item.Id == request.RequesterAssignmentId || item.Id == request.TargetAssignmentId).ToListAsync(ct);
        if (!assignments.Any(item => item.Id == request.RequesterAssignmentId && item.EmployeeId == request.RequesterEmployeeId && item.Status == ShiftAssignmentStatus.Published) ||
            !assignments.Any(item => item.Id == request.TargetAssignmentId && item.EmployeeId == request.TargetEmployeeId && item.Status == ShiftAssignmentStatus.Published))
            return ApiResponse<Guid>.Fail("تعيينات التبديل غير صالحة", ["SHIFT_SWAP_ASSIGNMENT_INVALID"]);
        var swap = new ShiftSwapRequest
        {
            RequesterEmployeeId = request.RequesterEmployeeId, RequesterAssignmentId = request.RequesterAssignmentId,
            TargetEmployeeId = request.TargetEmployeeId, TargetAssignmentId = request.TargetAssignmentId, Reason = request.Reason.Trim()
        };
        _db.ShiftSwapRequests.Add(swap);
        await _audit.WriteMutationAsync("SubmitShiftSwap", nameof(ShiftSwapRequest), swap.Id, null,
            new { swap.RequesterEmployeeId, swap.TargetEmployeeId, swap.Status }, swap.Reason, ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(swap.Id);
    }
}

public sealed record DecideShiftSwapCommand(Guid RequestId, bool Approve, string Reason, Guid ActorUserId, bool IsHrDecision, int ExpectedVersion)
    : IRequest<ApiResponse<bool>>, IHrAuthorizedRequest
{
    public string RequiredPermission => IsHrDecision ? HrPermissions.ShiftManage : HrPermissions.LeaveTeamReview;
    public HrAccessScope RequiredScope => IsHrDecision ? HrAccessScope.All : HrAccessScope.DirectTeam;
}

public sealed class DecideShiftSwapCommandHandler : IRequestHandler<DecideShiftSwapCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit;
    public DecideShiftSwapCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); }
    public async Task<ApiResponse<bool>> Handle(DecideShiftSwapCommand request, CancellationToken ct)
    {
        var swap = await _db.ShiftSwapRequests.SingleOrDefaultAsync(item => item.Id == request.RequestId, ct);
        if (swap is null) return ApiResponse<bool>.Fail("طلب التبديل غير موجود", ["SHIFT_SWAP_NOT_FOUND"]);
        if (swap.Version != request.ExpectedVersion) return ApiResponse<bool>.Fail("تم تعديل الطلب", ["CONCURRENCY_CONFLICT"]);
        var requesterUserId = await _db.EmployeeProfiles.Where(item => item.Id == swap.RequesterEmployeeId).Select(item => item.UserId).SingleAsync(ct);
        if (requesterUserId == request.ActorUserId) return ApiResponse<bool>.Fail("لا يمكن اعتماد طلبك", ["SELF_APPROVAL_FORBIDDEN"]);
        var before = swap.Status;
        if (!request.Approve)
        {
            swap.Status = ShiftSwapStatus.Rejected; swap.DecisionReason = request.Reason; swap.Version++;
        }
        else if (!request.IsHrDecision && swap.Status == ShiftSwapStatus.PendingManager)
        {
            swap.Status = ShiftSwapStatus.PendingHr; swap.ManagerDecisionByUserId = request.ActorUserId; swap.Version++;
        }
        else if (request.IsHrDecision && swap.Status == ShiftSwapStatus.PendingHr)
        {
            var original = await _db.ShiftAssignments.SingleAsync(item => item.Id == swap.RequesterAssignmentId, ct);
            var target = await _db.ShiftAssignments.SingleAsync(item => item.Id == swap.TargetAssignmentId, ct);
            original.Status = ShiftAssignmentStatus.Superseded; target.Status = ShiftAssignmentStatus.Superseded;
            _db.ShiftAssignments.AddRange(CloneReplacement(original, target.ShiftTemplateId, request.ActorUserId), CloneReplacement(target, original.ShiftTemplateId, request.ActorUserId));
            swap.Status = ShiftSwapStatus.Approved; swap.HrDecisionByUserId = request.ActorUserId; swap.Version++;
        }
        else return ApiResponse<bool>.Fail("قرار التبديل خارج الترتيب", ["APPROVAL_OUT_OF_ORDER"]);
        await _audit.WriteMutationAsync("DecideShiftSwap", nameof(ShiftSwapRequest), swap.Id,
            new { status = before }, new { swap.Status, swap.Version }, request.Reason, ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    private static ShiftAssignment CloneReplacement(ShiftAssignment original, Guid templateId, Guid actorId) => new()
    {
        EmployeeId = original.EmployeeId, ShiftTemplateId = templateId, EffectiveFrom = original.EffectiveFrom,
        EffectiveTo = original.EffectiveTo, Status = ShiftAssignmentStatus.Published, ReplacesAssignmentId = original.Id,
        Reason = "Approved shift swap", PublishedByUserId = actorId, PublishedAt = DateTime.UtcNow
    };
}
