using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Attendance.Corrections;

public sealed record SubmitAttendanceCorrectionCommand(Guid UserId, Guid AttendanceSessionId, DateTime? ProposedClockedInAt,
    DateTime? ProposedClockedOutAt, string Reason, string? EvidenceReference) : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceSelf; public HrAccessScope RequiredScope => HrAccessScope.Self; public Guid? ResourceUserId => UserId;
}
public sealed class SubmitAttendanceCorrectionCommandHandler : IRequestHandler<SubmitAttendanceCorrectionCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit;
    public SubmitAttendanceCorrectionCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); }
    public async Task<ApiResponse<Guid>> Handle(SubmitAttendanceCorrectionCommand request, CancellationToken ct)
    {
        var employee = await _db.EmployeeProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == request.UserId, ct);
        if (employee is null) return ApiResponse<Guid>.Fail("ملف الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);
        var session = await _db.AttendanceSessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.AttendanceSessionId && item.EmployeeId == employee.Id, ct);
        if (session is null) return ApiResponse<Guid>.Fail("جلسة الحضور غير موجودة", ["ATTENDANCE_SESSION_NOT_FOUND"]);
        if (await _db.AttendanceCorrections.AnyAsync(item => item.AttendanceSessionId == session.Id &&
            (item.State == AttendanceCorrectionState.PendingManager || item.State == AttendanceCorrectionState.PendingHr), ct))
            return ApiResponse<Guid>.Fail("يوجد تصحيح قيد المراجعة", ["ATTENDANCE_CORRECTION_PENDING"]);
        var proposedIn = request.ProposedClockedInAt ?? session.ClockedInAt; var proposedOut = request.ProposedClockedOutAt ?? session.ClockedOutAt;
        if (proposedOut.HasValue && proposedOut <= proposedIn) return ApiResponse<Guid>.Fail("أوقات التصحيح غير صالحة", ["ATTENDANCE_CORRECTION_TIME_INVALID"]);
        var before = JsonSerializer.Serialize(new { session.ClockedInAt, session.ClockedOutAt, session.State, session.Version });
        var correction = new AttendanceCorrection { EmployeeId = employee.Id, AttendanceSessionId = session.Id,
            ProposedClockedInAt = request.ProposedClockedInAt, ProposedClockedOutAt = request.ProposedClockedOutAt,
            Reason = request.Reason.Trim(), EvidenceReference = request.EvidenceReference, BeforeJson = before };
        _db.AttendanceCorrections.Add(correction);
        await _audit.WriteMutationAsync("SubmitAttendanceCorrection", nameof(AttendanceCorrection), correction.Id, null,
            new { correction.AttendanceSessionId, correction.ProposedClockedInAt, correction.ProposedClockedOutAt, correction.State }, correction.Reason, ct, request.UserId);
        await _db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(correction.Id);
    }
}

public sealed record DecideAttendanceCorrectionCommand(Guid CorrectionId, bool Approve, bool IsHrDecision, string Reason, Guid ActorUserId, int ExpectedVersion)
    : IRequest<ApiResponse<bool>>, IHrAuthorizedRequest
{
    public string RequiredPermission => IsHrDecision ? HrPermissions.AttendanceReview : HrPermissions.AttendanceTeamRead;
    public HrAccessScope RequiredScope => IsHrDecision ? HrAccessScope.All : HrAccessScope.DirectTeam;
}
public sealed class DecideAttendanceCorrectionCommandHandler : IRequestHandler<DecideAttendanceCorrectionCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit;
    public DecideAttendanceCorrectionCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); }
    public async Task<ApiResponse<bool>> Handle(DecideAttendanceCorrectionCommand request, CancellationToken ct)
    {
        var correction = await _db.AttendanceCorrections.Include(item => item.AttendanceSession)
            .SingleOrDefaultAsync(item => item.Id == request.CorrectionId, ct);
        if (correction is null) return ApiResponse<bool>.Fail("طلب التصحيح غير موجود", ["ATTENDANCE_CORRECTION_NOT_FOUND"]);
        if (correction.Version != request.ExpectedVersion) return ApiResponse<bool>.Fail("تم تعديل الطلب", ["CONCURRENCY_CONFLICT"]);
        if (correction.State is AttendanceCorrectionState.Approved or AttendanceCorrectionState.Rejected or AttendanceCorrectionState.Withdrawn)
            return ApiResponse<bool>.Fail("تم حسم الطلب", ["ATTENDANCE_CORRECTION_ALREADY_DECIDED"]);
        var employeeUserId = await _db.EmployeeProfiles.Where(item => item.Id == correction.EmployeeId).Select(item => item.UserId).SingleAsync(ct);
        if (employeeUserId == request.ActorUserId) return ApiResponse<bool>.Fail("لا يمكن اعتماد طلبك", ["SELF_APPROVAL_FORBIDDEN"]);
        var previousState = correction.State;
        if (!request.Approve)
        {
            correction.State = AttendanceCorrectionState.Rejected; correction.DecisionReason = request.Reason; correction.Version++;
        }
        else if (!request.IsHrDecision && correction.State == AttendanceCorrectionState.PendingManager)
        {
            correction.State = AttendanceCorrectionState.PendingHr; correction.ManagerDecisionByUserId = request.ActorUserId; correction.Version++;
        }
        else if (request.IsHrDecision && correction.State == AttendanceCorrectionState.PendingHr)
        {
            var session = correction.AttendanceSession!; var before = new { session.ClockedInAt, session.ClockedOutAt, session.State, session.Version };
            session.ClockedInAt = correction.ProposedClockedInAt ?? session.ClockedInAt; session.ClockedOutAt = correction.ProposedClockedOutAt ?? session.ClockedOutAt;
            if (session.ClockedOutAt.HasValue)
            {
                var shiftAssignment = await _db.ShiftAssignments.Include(item => item.ShiftTemplate).ThenInclude(item => item!.Segments)
                    .SingleOrDefaultAsync(item => item.Id == session.ShiftAssignmentId, ct);
                var segment = shiftAssignment?.ShiftTemplate is { } template
                    ? ShiftScheduleRules.SegmentForWorkDate(template.Segments, session.WorkDate)
                    : null;
                if (segment is not null)
                {
                    var scheduledStart = CairoTime.ToUtc(session.WorkDate.ToDateTime(TimeOnly.FromTimeSpan(segment.StartsAt)));
                    var scheduledEnd = CairoTime.ToUtc(session.WorkDate.ToDateTime(TimeOnly.FromTimeSpan(segment.EndsAt)));
                    if (segment.EndsAt <= segment.StartsAt) scheduledEnd = scheduledEnd.AddDays(1);
                    var breaks = await _db.AttendanceBreaks.Where(item => item.AttendanceSessionId == session.Id && item.EndedAt.HasValue).ToListAsync(ct);
                    var breakMinutes = breaks.Sum(item => (int)(item.EndedAt!.Value - item.StartedAt).TotalMinutes);
                    var result = AttendanceCalculator.Calculate(new(session.ClockedInAt, session.ClockedOutAt.Value, scheduledStart, scheduledEnd,
                        breakMinutes, shiftAssignment!.ShiftTemplate!.GraceMinutes, shiftAssignment.ShiftTemplate.OvertimeAfterMinutes));
                    session.WorkedMinutes = result.WorkedMinutes; session.LateMinutes = result.LateMinutes; session.EarlyLeaveMinutes = result.EarlyLeaveMinutes; session.OvertimeMinutes = result.OvertimeMinutes;
                }
                session.State = AttendanceSessionState.Corrected;
            }
            session.Version++; correction.State = AttendanceCorrectionState.Approved; correction.HrDecisionByUserId = request.ActorUserId;
            correction.AppliedAt = DateTime.UtcNow; correction.Version++;
            correction.AppliedJson = JsonSerializer.Serialize(new { before, after = new { session.ClockedInAt, session.ClockedOutAt, session.State, session.Version } });
        }
        else return ApiResponse<bool>.Fail("القرار خارج ترتيب الموافقات", ["APPROVAL_OUT_OF_ORDER"]);
        await _audit.WriteMutationAsync("DecideAttendanceCorrection", nameof(AttendanceCorrection), correction.Id,
            new { state = previousState }, new { correction.State, correction.Version, correction.AppliedAt }, request.Reason, ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }
}
